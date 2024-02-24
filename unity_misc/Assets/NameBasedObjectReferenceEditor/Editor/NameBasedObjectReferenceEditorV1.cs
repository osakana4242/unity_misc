using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System;

namespace NameBasedObjectReferenceEditors {
	public class NameBasedObjectReferenceEditorV1 : EditorWindow {
		static readonly Encoding TempFileEncoding = new System.Text.UTF8Encoding(false);
		Vector2 scrollPosition_;
		File[] fileArr_ = new File[0];
		string text_ = FileItem.LineHeader;
		string textPath_ = "";

		[MenuItem("Window/Osakana4242/NameBasedObjectReferenceEditorV1")]
		static void Init() {
			EditorWindow.GetWindow<NameBasedObjectReferenceEditorV1>();
		}

		static string[] ToGUIDArr(UnityEngine.Object[] objects) {
			List<string> guidList = new List<string>();
			for (int i = 0; i < objects.Length; ++i) {
				var item = objects[i];
				ProgressBarUtil.ThrowIfCanceled(i, objects.Length);
				var isValid = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item.GetInstanceID(), out var guid, out long _);
				if (!isValid) {
					continue;
				}
				guidList.Add(guid);
			}
			return guidList.ToArray();
		}

		static string[] GetFilePathArr(string[] guidArr) {
			var filePathList = new List<string>();
			while (0 < guidArr.Length) {
				var folders = new List<string>();
				for (int i = 0; i < guidArr.Length; ++i) {
					ProgressBarUtil.ThrowIfCanceled(i, guidArr.Length);
					var guid = guidArr[i];
					var path = AssetDatabase.GUIDToAssetPath(guid);
					if (AssetDatabase.IsValidFolder(path)) {
						folders.Add(path);
					} else {
						filePathList.Add(path);
					}
				}
				if (folders.Count <= 0) {
					break;
				}
				guidArr = AssetDatabase.FindAssets("", folders.ToArray());
			}
			return filePathList.ToArray();
		}

		static File[] CreateFileArr(string[] filePathArr) {
			var fileList = new List<File>();
			int lineIndex = 0;
			for (int i = 0; i < filePathArr.Length; ++i) {
				var path = filePathArr[i];
				ProgressBarUtil.ThrowIfCanceled(i, filePathArr.Length, path);
				if (!File.IsSupportedFile(path)) {
					continue;
				}
				var file = new File(lineIndex, path);
				if (file.LineCount <= 0) {
					continue;
				}
				lineIndex += file.LineCount;
				fileList.Add(file);
			}
			return fileList.ToArray();
		}

		void LoadFromSelection() {
			GUI.FocusControl(null);

			try {
				textPath_ = "";
				var guidArr = ToGUIDArr(Selection.objects);
				var filePathArr = GetFilePathArr(guidArr);
				fileArr_ = CreateFileArr(filePathArr);
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
			sb.Append(FileItem.LineHeader);
			sb.Append("\n");
			foreach (var file in fileArr_) {
				ProgressBarUtil.ThrowIfCanceled();
				file.WriteTo(sb);
			}
			text_ = sb.ToString();
			WriteTextIfNeeded();
		}

		void WriteTextIfNeeded() {
			if (string.IsNullOrEmpty(textPath_)) {
				return;
			}
			var text = this.text_;
			System.IO.File.WriteAllText(textPath_, text, TempFileEncoding);
		}

		void OpenWithTextEditor() {
			if (string.IsNullOrEmpty(textPath_)) {
				textPath_ = $"Temp/NameBasedObjectReferenceEdit_{System.DateTimeOffset.Now.ToString("yyyyMMddHHmmssff")}.tsv.txt";
			}
			WriteTextIfNeeded();
			var uri = new System.Uri(System.IO.Path.GetFullPath(textPath_));
			Application.OpenURL(uri.AbsoluteUri);
		}

		void Validate() {
			GUI.FocusControl(null);
			if (!string.IsNullOrEmpty(textPath_)) {
				text_ = System.IO.File.ReadAllText(textPath_, TempFileEncoding);
			}
			try {
				var lines = text_.
					Replace("\r\n", "\n").
					Split('\n').
					Where(_line => !_line.StartsWith("#", StringComparison.Ordinal)).
					ToArray().
					AsSpan();

				var lineIndex = 0;
				var nextFileArr = new File[fileArr_.Length];
				for (int fileIndex = 0; fileIndex < fileArr_.Length; ++fileIndex) {
					ProgressBarUtil.ThrowIfCanceled();
					var file = fileArr_[fileIndex];
					var sliced = lines.Slice(lineIndex, file.LineCount);
					nextFileArr[fileIndex] = file.ReadFrom(sliced);
					lineIndex += file.LineCount;
				}

				fileArr_ = nextFileArr;
				BuildText();
			} finally {
				EditorUtility.ClearProgressBar();
			}
		}

		bool CanApply() {
			if (0 < GetInValidCount()) {
				return false;
			}
			if (GetDirtyCount() <= 0) {
				return false;
			}
			return true;
		}

		int GetInValidCount() =>
			fileArr_.Sum(_item => _item.GetInValidCount());

		int GetDirtyCount() =>
			fileArr_.Sum(_item => _item.GetDirtyCount());

		void Apply() {
			GUI.FocusControl(null);
			try {
				Validate();

				if (0 < GetInValidCount()) {
					return;
				}

				if (GetDirtyCount() <= 0) {
					return;
				}

				try {
					for (int i = 0; i < fileArr_.Length; ++i) {
						ProgressBarUtil.ThrowIfCanceled();
						var item = fileArr_[i];
						item.Apply();
					}

					var nextList = new List<File>();
					for (int i = 0; i < fileArr_.Length; ++i) {
						ProgressBarUtil.ThrowIfCanceled();
						var item1 = fileArr_[i];
						var item2 = item1.Reload();
						nextList.Add(item2);
					}
					fileArr_ = nextList.ToArray();

				} finally {
					AssetDatabase.Refresh();
				}
			} finally {
				EditorUtility.ClearProgressBar();
			}
		}

		void OnGUI() {
			using (new EditorGUI.DisabledScope(Selection.objects.Length <= 0)) {
				if (GUILayout.Button($"Load From Selection (Selected: {Selection.objects.Length})")) {
					LoadFromSelection();
				}
			}

			using (new GUILayout.HorizontalScope()) {
				if (GUILayout.Button("Validate")) {
					Validate();
				}
				using (new EditorGUI.DisabledScope(!CanApply())) {
					if (GUILayout.Button($"Apply (Edited: {GetDirtyCount()})")) {
						Apply();
					}
				}
			}
			EditorGUILayout.IntField("Invalid Count", GetInValidCount());
			EditorGUILayout.IntField("Edited Count", GetDirtyCount());
			if (GUILayout.Button("Open With Text Editor")) {
				OpenWithTextEditor();
			}
			var hasTextFile = !string.IsNullOrEmpty(textPath_);
			if (hasTextFile) {
				EditorGUILayout.LabelField(textPath_);
			}
			using (var scrollViewScope = new EditorGUILayout.ScrollViewScope(scrollPosition_)) {
				scrollPosition_ = scrollViewScope.scrollPosition;
				using (new EditorGUI.DisabledScope(hasTextFile)) {
					text_ = EditorGUILayout.TextArea(text_);
				}
			}
		}

		class File {
			static readonly Encoding AssetFileEncoding = TempFileEncoding;
			static readonly string UTF8Bom = AssetFileEncoding.GetString(new byte[] { 0xEF, 0xBB, 0xBF });
			static readonly string Header = "%YAML 1.1";

			const string GroupLabel = "label";
			const string GroupGUID = "guid";
			const string GroupType = "type";
			const string GroupFileID = "fileID";
			static readonly Regex regex = new Regex($@"(?<{GroupLabel}>(\w+):\s+|(-.*))?{{fileID:\s+(?<{GroupFileID}>\w+),\s+guid:\s+(?<{GroupGUID}>\w+),\s+type:\s+(?<{GroupType}>\w+)}}");

			public readonly int index;
			public readonly string path;
			readonly FileItem[] itemArr_;
			readonly string text_;

			public File(int index, string path) {
				this.index = index;
				this.path = path;
				text_ = System.IO.File.ReadAllText(path, AssetFileEncoding);
				itemArr_ = CreateItemArr(path, index, text_);
			}

			File(File other, FileItem[] itemArr) {
				this.index = other.index;
				this.path = other.path;
				this.text_ = other.text_;
				this.itemArr_ = itemArr;
			}

			public int LineCount => itemArr_.Length;

			public static bool IsSupportedFile(string path) {
				using (var file = System.IO.File.Open(path, System.IO.FileMode.Open, System.IO.FileAccess.Read)) {
					byte[] buffer = new byte[32];
					var readedCount = file.Read(buffer, 0, buffer.Length);
					if (readedCount <= 0) return false;
					var str = AssetFileEncoding.GetString(buffer, 0, readedCount);
					var hasBom = str.StartsWith(UTF8Bom, StringComparison.Ordinal);
					var headerIndex = str.IndexOf(Header, StringComparison.Ordinal);
					if (hasBom) {
						return headerIndex == 1;
					} else {
						return headerIndex == 0;
					}
				}
			}

			public File Reload() {
				return new File(index, path);
			}

			public bool IsValid() {
				var invalidIndex = System.Array.FindIndex(itemArr_, (_item) => !_item.IsValid());
				return invalidIndex == -1;
			}

			public bool IsDirty() {
				var dirtyIndex = System.Array.FindIndex(itemArr_, (_item) => _item.IsDirty());
				return dirtyIndex != -1;
			}

			public int GetInValidCount() =>
				itemArr_.Count((_item) => !_item.IsValid());

			public int GetDirtyCount() =>
				itemArr_.Count((_item) => _item.IsDirty());

			static FileItem[] CreateItemArr(string ownerPath, int index, string text) {
				var matches = regex.Matches(text);
				var list = new List<FileItem>();
				for (var i = 0; i < matches.Count; ++i) {
					var item = matches[i];
					var groups = item.Groups;
					// Debug.Log($"i: {i}, match: {item}, label: {groups[GroupLabel]}, fileID: {groups[GroupFileID]}, guid: {groups[GroupGUID]}, type: {groups[GroupType]}");
					var fileItem = FileItem.CreateFromGUID(index + i, ownerPath, groups[GroupLabel].ToString(), groups[GroupGUID].ToString());
					list.Add(fileItem);
				}
				return list.ToArray();
			}

			public void Apply() {
				if (!IsValid()) {
					throw new System.InvalidOperationException("Can't Apply");
				}
				var i = 0;
				var text = regex.Replace(text_, (_m) => {
					var item = itemArr_[i];
					var next = $"{_m.Groups[GroupLabel]}{{fileID: {_m.Groups[GroupFileID]}, guid: {item.nextGUID}, type: {_m.Groups[GroupType]}}}";
					++i;
					return next;
				});
				System.IO.File.WriteAllText(path, text, AssetFileEncoding);
			}

			public void WriteTo(StringBuilder sb) {
				foreach (var item in itemArr_) {
					sb.Append(item.ToLine());
					sb.Append("\n");
				}
			}

			public File ReadFrom(Span<string> lines) {
				var lines2 = new List<FileItem>(itemArr_.Length);
				for (int i = 0; i < lines.Length; ++i) {
					var line = lines[i];
					var item = itemArr_[lines2.Count];
					lines2.Add(item.ReadFrom(line));
				}
				return new File(this, lines2.ToArray());
			}
		}

		class FileItem {
			public readonly static string LineHeader =
				"# ファイル参照の編集手順.\n" +
				"# 1. ファイルまたはフォルダーを選択し Load From Selection を選択する\n" +
				"# 2. refFilePath 列を編集する\n" +
				"# 3. Validate を選択する\n" +
				"# 4. Validate で問題が検出されなければ Apply が有効になる\n" +
				"# 5. Apply を選択する\n" +
				"# \n" +
				"# index\townerFilePath\tlabel\treFilePath";
			const int ColIndexIndex = 0;
			const int ColIndexOwnerPath = 1;
			const int ColIndexLabel = 2;
			const int ColIndexPath = 3;
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

			public FileItem ReadFrom(string line) {
				var cols = line.Split('\t');
				var inIndex = cols[ColIndexIndex];
				var inOwnerPath = cols[ColIndexOwnerPath];
				var inLabel = cols[ColIndexLabel];
				var inFileName = cols[ColIndexPath];
				if (this.index.ToString() != inIndex) {
					throw new System.ArgumentException($"index のずれを検出. 期待する index: {this.index}, 入力された index: {inIndex}");
				}
				return new FileItem(this.index, this.ownerPath, this.label, this.path, inFileName);
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

		static class ProgressBarUtil {
			public static string title = nameof(NameBasedObjectReferenceEditorV1);
			public static string body = "";
			public static float progress = 0;
			public static void ThrowIfCanceled(int index, int length, string suffix = "") {
				if (length <= 0) {
					progress = 0;
				} else {
					progress = index / (float)length;
				}
				body = $"{index} / {length} {suffix}";
				ThrowIfCanceled();
			}
			public static void ThrowIfCanceled() {
				var canceled = EditorUtility.DisplayCancelableProgressBar(title, body, progress);
				if (!canceled) {
					return;
				}
				throw new System.OperationCanceledException();
			}
		}
	}

	readonly struct Span<T> {
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

	static class SpanExt {
		public static Span<T> AsSpan<T>(this IList<T> self) =>
			new Span<T>(self, 0, self.Count);
	}
}
