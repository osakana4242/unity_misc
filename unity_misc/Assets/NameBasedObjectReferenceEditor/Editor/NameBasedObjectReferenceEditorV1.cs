using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System;

namespace Osakana4242 {
	public class NameBasedObjectReferenceEditorV1 : EditorWindow {

		string rightText_ = "";
		File[] workList_ = new File[0];

		[MenuItem("Window/Osakana4242/NameBasedObjectReferenceEditorV1")]
		static void Init() {
			EditorWindow.GetWindow<NameBasedObjectReferenceEditorV1>();
		}

		void LoadFromSelection() {
			GUI.FocusControl(null);
			AssetDatabase.Refresh();

			try {
				var list = Selection.objects.
					Select(_item => {
						var isValid = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(_item.GetInstanceID(), out var guid, out long _);
						return (isValid, guid);
					}).
					Where(_item => _item.isValid).
					Select(_item => (_item.guid, path: AssetDatabase.GUIDToAssetPath(_item.guid)));

				var workList2 = new List<File>();
				// load	
				foreach (var item in list) {
					ThrowIfCanceled();
					var item2 = new File(item.guid, item.path);
					workList2.Add(item2);
				}
				this.workList_ = workList2.ToArray();
				BuildText();
			} catch (System.OperationCanceledException) {
				EditorUtility.DisplayDialog("Canceled", "Canceled", "OK");
			} catch (System.Exception ex) {
				Debug.LogException(ex);
			} finally {
				EditorUtility.ClearProgressBar();
			}
			GUI.FocusControl(null);
		}

		void BuildText() {
			var sb = new System.Text.StringBuilder();
			foreach (var file in workList_) {
				ThrowIfCanceled();
				file.Write(sb);
			}
			rightText_ = sb.ToString();
		}

		void ThrowIfCanceled() {
			var canceled = EditorUtility.DisplayCancelableProgressBar("title", "body", 0);
			if (!canceled) {
				return;
			}
			throw new System.OperationCanceledException();
		}

		void Apply() {
			GUI.FocusControl(null);
			try {
				var lines = rightText_.Split('\n').AsSpan();
				var index = 0;

				var nextWorkList = new File[workList_.Length];
				for (int i = 0; i < workList_.Length; ++i) {
					ThrowIfCanceled();
					var item = workList_[i];
					var span = lines.Slice(index, item.LineCount);
					nextWorkList[i] = item.Read(span);
					index += item.LineCount;
				}

				workList_ = nextWorkList;
				BuildText();

				var canApply = System.Array.FindIndex(workList_, _item => !_item.CanApply()) == -1;
				if (!canApply)
					return;

				try {
					for (int i = 0; i < workList_.Length; ++i) {
						ThrowIfCanceled();
						var item = workList_[i];
						item.Apply();
					}
				} finally {
					AssetDatabase.Refresh();
				}
			} finally {
				EditorUtility.ClearProgressBar();
			}
		}

		void OnGUI() {
			using (new GUILayout.HorizontalScope()) {
				if (GUILayout.Button("Load")) {
					LoadFromSelection();
				}
				if (GUILayout.Button("Apply")) {
					Apply();
				}
			}
			rightText_ = EditorGUILayout.TextArea(rightText_);

		}

		class File {
			static readonly Regex regex = new Regex(@"((\w+):\s+)?{fileID:\s+(\w+),\s+guid:\s+(\w+),\s+type:\s+(\w+)}");
			public readonly string guid;
			public readonly string path;
			readonly FileItem[] itemArr;
			readonly string text_;

			public File(string guid, string path) {
				this.guid = guid;
				this.path = path;
				text_ = System.IO.File.ReadAllText(path);
				var matches = regex.Matches(text_);
				var list = new List<FileItem>();
				for (var i = 0; i < matches.Count; ++i) {
					var item = matches[i];
					var groups = item.Groups;
					var label = item.Groups[2].ToString();
					var itemPrefix = item.Groups[3].ToString();
					var itemGUID = item.Groups[4].ToString();
					Debug.Log($"i: {i}, match: {item}, 2: {label}, 3: {itemPrefix}, 4: {itemGUID}");
					var fileItem = FileItem.CreateFromGUID(label, itemGUID);
					list.Add(fileItem);
				}
				itemArr = list.ToArray();
			}

			File(File other, FileItem[] itemArr) {
				this.guid = other.guid;
				this.path = other.path;
				this.text_ = other.text_;
				this.itemArr = itemArr;
			}

			public bool CanApply() {
				var invalidIndex = System.Array.FindIndex(itemArr, (_item) => !_item.IsValid());
				return invalidIndex == -1;
			}

			public void Apply() {
				if (!CanApply()) {
					throw new System.InvalidOperationException("Can't Apply");
				}
				var i = 0;
				var text = regex.Replace(text_, (_m) => {
					var item = itemArr[i];
					var next = $"{{fileID: {_m.Groups[1]}, guid: {item.nextGUID}, type: {_m.Groups[3]}}}";
					++i;
					return next;
				});
				System.IO.File.WriteAllText(path, text, Encoding.UTF8);
			}

			public int LineCount => itemArr.Length;

			public string[] ToLines() {
				var lineList = new List<string>();
				foreach (var item in itemArr) {
					lineList.Add($"{path}\t_{item.label}\t{item.nextPath}");
				}
				return lineList.ToArray();
			}

			public void Write(StringBuilder sb) {
				foreach (var line in ToLines()) {
					sb.Append($"{line}\n");
				}
			}

			public File Read(Span<string> lines) {
				var lines2 = new List<FileItem>(itemArr.Length);
				for (int i = 0; i < lines.Length; ++i) {
					var line = lines[i];
					if (line.StartsWith("#", StringComparison.Ordinal))
						continue;
					var cols = line.Split('\t');
					var ownerFileName = cols[0];
					var label = cols[1];
					var fileName = cols[2];

					var item = itemArr[lines2.Count];
					lines2.Add(new FileItem(label, item.path, fileName));
				}
				return new File(this, lines2.ToArray());
			}
		}

		class FileItem {
			const string suffix = ":(Not Found)";
			public readonly string path;
			public readonly string nextPath;
			public readonly string nextGUID;
			public readonly string label;

			public FileItem(string label, string path, string nextPath) {
				this.label = label;
				this.path = path;
				if (string.IsNullOrEmpty(nextPath)) {
					this.nextPath = "";
				} else {
					string sanytizedPath;
					if (nextPath.EndsWith(suffix)) {
						sanytizedPath = nextPath.Substring(0, nextPath.Length - suffix.Length);
					} else {
						sanytizedPath = nextPath;
					}
					nextGUID = AssetDatabase.AssetPathToGUID(sanytizedPath);
					if (string.IsNullOrEmpty(nextGUID)) {
						this.nextPath = $"{sanytizedPath}{suffix}";
					} else {
						this.nextPath = sanytizedPath;
					}
				}
			}

			public bool IsValid() => !nextPath.EndsWith(suffix);

			public static FileItem CreateFromGUID(string label, string guid) {
				var path = AssetDatabase.GUIDToAssetPath(guid);
				return new FileItem(label, path, path);
			}
		}
	}
}
