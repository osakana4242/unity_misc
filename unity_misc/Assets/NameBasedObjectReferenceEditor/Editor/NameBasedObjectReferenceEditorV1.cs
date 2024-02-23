using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System;

namespace NameBasedObjectReferenceEditorV1 {
	public class NameBasedObjectReferenceEditorV1 : EditorWindow {
		string rightText_ = "";
		File[] workList_ = new File[0];

		[MenuItem("Window/Osakana4242/NameBasedObjectReferenceEditorV1")]
		static void Init() {
			EditorWindow.GetWindow<NameBasedObjectReferenceEditorV1>();
		}

		void LoadFromSelection() {
			GUI.FocusControl(null);

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
				int index = 0;
				foreach (var item in list) {
					ThrowIfCanceled();
					var item2 = new File(index, item.guid, item.path);
					index += item2.LineCount;
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

		void Validate() {
			GUI.FocusControl(null);
			try {
				var lines = rightText_.
					Replace("\r\n", "\n").
					Split('\n').
					AsSpan();
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
			} finally {
				EditorUtility.ClearProgressBar();
			}
		}

		bool CanApply() {
			return (GetInValidCount() == 0) && (0 < GetDirtyCount());
		}

		int GetInValidCount() =>
			workList_.Sum(_item => _item.GetInValidCount());

		int GetDirtyCount() =>
			workList_.Sum(_item => _item.GetDirtyCount());

		void Apply() {
			GUI.FocusControl(null);
			try {
				Validate();

				if (0 < GetInValidCount()) {
					Debug.Log("can't apply");
					return;
				}

				if (GetDirtyCount() <= 0) {
					Debug.Log("no changes");
					return;
				}

				try {
					for (int i = 0; i < workList_.Length; ++i) {
						ThrowIfCanceled();
						var item = workList_[i];
						item.Apply();
					}

					var nextList = new List<File>();
					for (int i = 0; i < workList_.Length; ++i) {
						ThrowIfCanceled();
						var item1 = workList_[i];
						var item2 = item1.Reload();
						nextList.Add(item2);
					}
					workList_ = nextList.ToArray();

				} finally {
					AssetDatabase.Refresh();
				}
			} finally {
				EditorUtility.ClearProgressBar();
			}
		}
		Vector2 scrollPosition_;

		void OnGUI() {
			using (new GUILayout.HorizontalScope()) {
				if (GUILayout.Button("Load")) {
					LoadFromSelection();
				}
				if (GUILayout.Button("Validate")) {
					Validate();
				}
				using (new EditorGUI.DisabledScope(!CanApply())) {
					if (GUILayout.Button("Apply")) {
						Apply();
					}
				}
			}
			EditorGUILayout.IntField("Invalid Count", GetInValidCount());
			EditorGUILayout.IntField("Dirty Count", GetDirtyCount());
			using (var scrollViewScope = new EditorGUILayout.ScrollViewScope(scrollPosition_)) {
				scrollPosition_ = scrollViewScope.scrollPosition;
				rightText_ = EditorGUILayout.TextArea(rightText_);
			}
		}

		class File {

			const string GroupLabel = "label";
			const string GroupGUID = "guid";
			const string GroupType = "type";
			const string GroupFileID = "fileID";
			static readonly Regex regex = new Regex($@"(?<{GroupLabel}>(\w+):\s+|(-.*))?{{fileID:\s+(?<{GroupFileID}>\w+),\s+guid:\s+(?<{GroupGUID}>\w+),\s+type:\s+(?<{GroupType}>\w+)}}");
			public readonly int index;
			public readonly string guid;
			public readonly string path;
			readonly FileItem[] itemArr;
			readonly string text_;

			public File(int index, string guid, string path) {
				this.index = index;
				this.guid = guid;
				this.path = path;
				text_ = System.IO.File.ReadAllText(path);
				var matches = regex.Matches(text_);
				var list = new List<FileItem>();
				for (var i = 0; i < matches.Count; ++i) {
					var item = matches[i];
					var groups = item.Groups;
					Debug.Log($"i: {i}, match: {item}, label: {groups[GroupLabel]}, fileID: {groups[GroupFileID]}, guid: {groups[GroupGUID]}, type: {groups[GroupType]}");
					var fileItem = FileItem.CreateFromGUID(index + i, path, groups[GroupLabel].ToString(), groups[GroupGUID].ToString());
					list.Add(fileItem);
				}
				itemArr = list.ToArray();
			}

			File(File other, FileItem[] itemArr) {
				this.index = other.index;
				this.guid = other.guid;
				this.path = other.path;
				this.text_ = other.text_;
				this.itemArr = itemArr;
			}

			public File Reload() {
				return new File(index, guid, path);
			}

			public bool IsValid() {
				var invalidIndex = System.Array.FindIndex(itemArr, (_item) => !_item.IsValid());
				return invalidIndex == -1;
			}

			public bool IsDirty() {
				var dirtyIndex = System.Array.FindIndex(itemArr, (_item) => _item.IsDirty());
				return dirtyIndex != -1;
			}

			public int GetInValidCount() =>
				itemArr.Count((_item) => !_item.IsValid());

			public int GetDirtyCount() =>
				itemArr.Count((_item) => _item.IsDirty());

			public void Apply() {
				if (!IsValid()) {
					throw new System.InvalidOperationException("Can't Apply");
				}
				var i = 0;
				var text = regex.Replace(text_, (_m) => {
					var item = itemArr[i];
					var next = $"{_m.Groups[GroupLabel]}{{fileID: {_m.Groups[GroupFileID]}, guid: {item.nextGUID}, type: {_m.Groups[GroupType]}}}";
					++i;
					return next;
				});
				System.IO.File.WriteAllText(path, text, Encoding.UTF8);
			}

			public int LineCount => itemArr.Length;

			public void Write(StringBuilder sb) {
				foreach (var item in itemArr) {
					sb.Append($"{item.ToLine()}\n");
				}
			}

			public File Read(Span<string> lines) {
				var lines2 = new List<FileItem>(itemArr.Length);
				for (int i = 0; i < lines.Length; ++i) {
					var line = lines[i];
					if (line.StartsWith("#", StringComparison.Ordinal))
						continue;
					var item = itemArr[lines2.Count];
					lines2.Add(item.CreateFromLine(line));
				}
				return new File(this, lines2.ToArray());
			}
		}

		class FileItem {
			const string suffix = ":(Not Found)";
			public readonly int index;
			public readonly string ownerPath;
			public readonly string path;
			public readonly string nextPath;
			public readonly string nextGUID;
			public readonly string label;

			public FileItem(int index, string ownerPath, string label, string path, string nextPath) {
				this.index = index;
				this.ownerPath = ownerPath;
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

			public FileItem CreateFromLine(string line) {
				var cols = line.Split('\t');
				var index = cols[0];
				var ownerPath = cols[1];
				var label = cols[2];
				var fileName = cols[3];
				if (this.index.ToString() != index) {
					throw new System.ArgumentException($"index の差異を検出. index1: {this.index}, index2: {index}");
				}
				if (this.ownerPath != ownerPath) {
					throw new System.ArgumentException($"ownerPath の差異を検出. ownerPath1: {this.ownerPath}, ownerPath2: {ownerPath}");
				}
				if (this.label != label) {
					throw new System.ArgumentException($"label の差異を検出. label1: {this.label}, label2: {label}");
				}
				return new FileItem(this.index, this.ownerPath, this.label, this.path, fileName);
			}

			public string ToLine() {
				return $"{index}\t{ownerPath}\t{label}\t{nextPath}";
			}

			public bool IsValid() {
				return !nextPath.EndsWith(suffix);
			}

			public bool IsDirty() {
				return path != nextPath;
			}

			public static FileItem CreateFromGUID(int index, string ownerPath, string label, string guid) {
				var path = AssetDatabase.GUIDToAssetPath(guid);
				return new FileItem(index, ownerPath, label, path, path);
			}
		}
	}

	public readonly struct Span<T> {
		readonly IList<T> list_;
		readonly int start_;
		readonly int length_;

		public Span(IList<T> list, int start, int length) {
			Validate(list.Count, start, length);

			list_ = list;
			start_ = start;
			length_ = length;
		}

		public int Length =>
			length_;

		public T this[int index] =>
			list_[start_ + index];

		static void Validate(int leftLength, int rightStart, int rightLength) {
			if (rightStart < 0) {
				throw new System.IndexOutOfRangeException($"start: {rightStart}, range: [0, {leftLength})");
			}
			if (leftLength <= rightStart) {
				throw new System.IndexOutOfRangeException($"start: {rightStart}, range: [0, {leftLength})");
			}
			var last = rightStart + rightLength - 1;
			if (leftLength <= last) {
				throw new System.IndexOutOfRangeException($"last: {last}, range: [0, {leftLength})");
			}
		}

		public Span<T> Slice(int start, int length) {
			Validate(length_, start, length);
			return new Span<T>(list_, start_ + start, length);
		}
	}

	public static class SpanExt {
		public static Span<T> AsSpan<T>(this IList<T> self) =>
			new Span<T>(self, 0, self.Count);
	}
}
