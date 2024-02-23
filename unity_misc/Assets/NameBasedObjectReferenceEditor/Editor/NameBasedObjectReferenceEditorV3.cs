using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System;
using System.Reflection;
using NUnit.Compatibility;

namespace Osakana4242 {
	public readonly struct Span<T> {
		readonly IList<T> list_;
		readonly int start_;
		public readonly int Length;

		public Span(IList<T> list, int start, int length) {
			var _ = list[start];
			_ = list[start + length - 1];

			list_ = list;
			start_ = start;
			Length = length;
		}

		public T this[int index] =>
			list_[start_ + index];

		public Span<T> Slice(int start, int length) =>
			new Span<T>(list_, start_ + start, length);
	}

	public static class SpanExt {
		public static Span<T> AsSpan<T>(this IList<T> self) =>
			new Span<T>(self, 0, self.Count);
	}

	public class NameBasedObjectReferenceEditorV3 : EditorWindow {

		string rightText_ = "";
		File[] workList_ = new File[0];
		Vector2 scrollPosition_;
		int progressIndex_;
		int progressCount_;

		[MenuItem("Window/Osakana4242/NameBasedObjectReferenceEditorV3")]
		static void Init() {
			EditorWindow.GetWindow<NameBasedObjectReferenceEditorV3>();
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
					Select(_item => (_item.guid, path: AssetDatabase.GUIDToAssetPath(_item.guid))).
					ToArray();

				var workList2 = new List<File>();
				// load	
				progressIndex_ = 0;
				progressCount_ = list.Length;
				foreach (var item in list) {
					++progressIndex_;
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
			var progress = progressIndex_ / (float)progressCount_;
			var canceled = EditorUtility.DisplayCancelableProgressBar("title", $"{progressIndex_} / {progressCount_}", progress);
			if (!canceled) return;
			throw new System.OperationCanceledException();
		}

		void Validate() {
			Apply(isDryRun: true);
		}

		void Apply() {
			Apply(isDryRun: false);
		}

		void Apply(bool isDryRun) {
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
				if (isDryRun)
					return;
				var canApply = System.Array.FindIndex(workList_, _item => !_item.CanApply()) == -1;
				if (!canApply)
					return;

				try {
					var isDirty = false;
					for (int i = 0; i < workList_.Length; ++i) {
						ThrowIfCanceled();
						var item = workList_[i];
						isDirty |= item.Apply();
					}
					if (isDirty) {
						AssetDatabase.SaveAssets();
					}
				} finally {
					AssetDatabase.Refresh();
				}
			} finally {
				EditorUtility.ClearProgressBar();
			}
		}

		void OnGUI() {
			using (new EditorGUILayout.HorizontalScope()) {
				if (GUILayout.Button("Load")) {
					LoadFromSelection();
				}
				if (GUILayout.Button("Validate")) {
					Validate();
				}
				if (GUILayout.Button("Apply")) {
					Apply();
				}
			}
			using (var scrollScope = new EditorGUILayout.ScrollViewScope(scrollPosition_)) {
				scrollPosition_ = scrollScope.scrollPosition;
				rightText_ = EditorGUILayout.TextArea(rightText_);
			}
		}

		class File {
			public readonly string guid;
			public readonly string path;
			readonly TargetField[] fieldArr_;
			readonly UnityEngine.Object object_;
			readonly SerializedObject so_;

			public File(string guid, string path) {
				this.guid = guid;
				this.path = path;
				object_ = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
				so_ = new SerializedObject(object_);

				var fieldList = new List<TargetField>();
				LoadBySerializedObject(so_, fieldList);
				//LoadByObject(object_, fieldList);
				fieldArr_ = fieldList.ToArray();
			}

			File(File other, TargetField[] itemArr) {
				this.guid = other.guid;
				this.path = other.path;
				this.object_ = other.object_;
				this.so_ = other.so_;
				this.fieldArr_ = itemArr;
			}

			static void LoadBySerializedObject(SerializedObject so, List<TargetField> outFieldList) {
				for (var sp = so.GetIterator(); sp.Next(true);) {
					if (sp.propertyType != SerializedPropertyType.ObjectReference) {
						continue;
					}

					var sp2 = so.FindProperty(sp.propertyPath);
					var accessor = new PropertyAccessor(sp2);
					var targetField = TargetField.Create(accessor);
					outFieldList.Add(targetField);
				}
			}

			static void LoadByObject(UnityEngine.Object inst, List<TargetField> outFieldList) {
				LoadObject(inst, outFieldList, new List<object>());
			}

			static void LoadObject(object inst, List<TargetField> outFieldList, List<object> depthList) {
				if (depthList.Count == 0) {
					var root = (UnityEngine.Object)inst;
					Debug.Log($"root: {root.name}");
				}

				if (127 < depthList.Count)
					throw new System.Exception();
				if (depthList.Contains(inst))
					return;
				try {
					depthList.Add(inst);

					var list = inst as IList;
					if (null != list) {
						for (int i = 0; i < list.Count; ++i) {
							var item = list[i];
							LoadObject(item, outFieldList, depthList);
						}
					} else {
						var instType = inst.GetType();
						var fields = instType.GetFields(
							System.Reflection.BindingFlags.Instance |
							System.Reflection.BindingFlags.NonPublic |
							System.Reflection.BindingFlags.Public
						);

						for (int i = 0; i < fields.Length; ++i) {
							var field = fields[i];
							LoadField(inst, field, outFieldList, depthList);
						}
					}
				} finally {
					depthList.RemoveAt(depthList.Count - 1);
				}
			}

			static void LoadField(object owner, FieldInfo field, List<TargetField> outFieldList, List<object> depthList) {
				var fieldType = field.FieldType;
				if (!IsSerializable(field))
					return;
				if (fieldType.IsValueType)
					return;


				if (FieldElementAccessor.CanCreate(field.FieldType)) {
					var list = FieldElementAccessor.CreateAllElement(owner, field);
					for (int i = 0; i < list.Length; ++i) {
						var accessor = list[i];
						var item = TargetField.Create(accessor);
						outFieldList.Add(item);
					}
					return;
				}

				if (FieldAccessor.CanCreate(field.FieldType)) {
					var item = TargetField.Create(new FieldAccessor(owner, field));
					outFieldList.Add(item);
					return;
				}

				var value = field.GetValue(owner);
				LoadObject(value, outFieldList, depthList);
			}

			static bool IsSerializable(FieldInfo field) {
				var fieldType = field.FieldType;
				if (field.IsDefined(typeof(System.NonSerializedAttribute)))
					return false;
				if (!field.IsPublic && !field.IsDefined(typeof(UnityEngine.SerializeField)))
					return false;

				if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType)) {
					return true;
				} else {
					if (!fieldType.IsDefined(typeof(System.SerializableAttribute)))
						return false;
					return true;
				}
			}

			public bool CanApply() {
				var invalidIndex = System.Array.FindIndex(fieldArr_, (_item) => !_item.IsValid());
				return invalidIndex == -1;
			}

			public bool Apply() {
				if (!CanApply()) {
					throw new System.InvalidOperationException("Can't Apply");
				}
				var isDirty = false;
				so_.Update();
				for (int i = 0; i < fieldArr_.Length; ++i) {
					var field = fieldArr_[i];
					isDirty |= field.Apply();
				}
				so_.ApplyModifiedProperties();
				if (isDirty) {
					EditorUtility.SetDirty(object_);
				}
				return isDirty;
			}

			public int LineCount => fieldArr_.Length;

			public string[] ToLines() {
				var lineList = new List<string>();
				foreach (var item in fieldArr_) {
					lineList.Add($"{path}\t{item.Label}\t{item.nextPath}");
				}
				return lineList.ToArray();
			}

			public void Write(StringBuilder sb) {
				foreach (var line in ToLines()) {
					sb.Append($"{line}\n");
				}
			}

			public File Read(Span<string> lines) {
				var fieldList2 = new List<TargetField>(fieldArr_.Length);
				for (int i = 0; i < lines.Length; ++i) {
					var line = lines[i];
					if (line.StartsWith("#", StringComparison.Ordinal))
						continue;
					var cols = line.Split('\t');
					var ownerFileName = cols[0];
					var label = cols[1];
					var fileName = cols[2];
					var field1 = fieldArr_[fieldList2.Count];
					if (field1.Label != label)
						throw new System.ArgumentException("テキストのずれを検出");
					var field2 = new TargetField(field1.accessor, fileName);
					fieldList2.Add(field2);
				}
				return new File(this, fieldList2.ToArray());
			}


		}

		abstract class Accessor {
			public abstract string GetLabel();
			public abstract UnityEngine.Object Get();
			public abstract void Set(UnityEngine.Object value);
			public override string ToString() => GetLabel();
		}

		class PropertyAccessor : Accessor {
			readonly SerializedProperty sp;
			public PropertyAccessor(SerializedProperty sp) : base() {
				this.sp = sp;
			}
			public override string GetLabel() =>
				sp.propertyPath;
			public override UnityEngine.Object Get() =>
				sp.objectReferenceValue;
			public override void Set(UnityEngine.Object value) =>
				sp.objectReferenceValue = value;
		}
		class FieldAccessor : Accessor {
			readonly object owner;
			readonly FieldInfo field;
			public FieldAccessor(object owner, FieldInfo field) {
				this.owner = owner;
				this.field = field;
			}
			public override string GetLabel() =>
				field.Name;
			public override UnityEngine.Object Get() =>
				(UnityEngine.Object)field.GetValue(owner);
			public override void Set(UnityEngine.Object value) =>
				field.SetValue(owner, value);
			public static bool CanCreate(System.Type type) {
				if (typeof(UnityEngine.Object).IsAssignableFrom(type))
					return true;
				return false;
			}
		}
		class FieldElementAccessor : Accessor {
			readonly object owner;
			readonly FieldInfo field;
			readonly int index;

			public static FieldElementAccessor[] CreateAllElement(object owner, FieldInfo field) {
				var valueOrList = field.GetValue(owner);
				var list = (IList)valueOrList;
				var fieldList = new List<FieldElementAccessor>();
				for (int i = 0; i < list.Count; ++i) {
					var item = new FieldElementAccessor(owner, field, i);
					fieldList.Add(item);
				}
				return fieldList.ToArray();
			}

			public FieldElementAccessor(object owner, FieldInfo field, int index) {
				this.owner = owner;
				this.field = field;
				this.index = index;
			}
			IList GetList() => (IList)field.GetValue(owner);
			public override string GetLabel() =>
				$"{field.Name}[{index}]";
			public override UnityEngine.Object Get() =>
				(UnityEngine.Object)GetList()[index];
			public override void Set(UnityEngine.Object value) =>
				GetList()[index] = value;
			public static bool CanCreate(System.Type type) {
				if (IsUnityObjectList(type))
					return true;
				return false;
			}
			static bool IsUnityObjectList(System.Type type) {
				System.Type elementType;
				if (type.IsArray) {
					elementType = type.GetElementType();
				} else {
					if (!type.IsGenericType)
						return false;
					if (!typeof(IList<>).IsAssignableFrom(type.GetGenericTypeDefinition()))
						return false;
					elementType = type.GenericTypeArguments[0];
				}

				if (!typeof(UnityEngine.Object).IsAssignableFrom(elementType))
					return false;
				return true;
			}
		}

		class TargetField {
			const string SUFFIX_NOT_FOUND = ":(Not Found)";
			const string SUFFIX_NULL = ":(Null)";
			public readonly string nextPath;
			public readonly string nextGUID;
			public readonly Accessor accessor;
			public string Label => accessor.GetLabel();

			public static TargetField Create(Accessor accessor) {
				var unityObject = accessor.Get();
				var path = GetPath(unityObject);
				return new TargetField(accessor, path);
			}

			public string CurrentPath =>
				GetPath(accessor.Get());

			static string SanytizePath(string path) {
				if (path.EndsWith(SUFFIX_NULL)) {
					return path.Substring(0, path.Length - SUFFIX_NULL.Length);
				} else if (path.EndsWith(SUFFIX_NOT_FOUND)) {
					return path.Substring(0, path.Length - SUFFIX_NOT_FOUND.Length);
				} else {
					return path;
				}
			}

			public TargetField(Accessor accessor, string nextPath) {
				this.accessor = accessor;
				var sanytizedPath = SanytizePath(nextPath);
				if (string.IsNullOrEmpty(sanytizedPath)) {
					this.nextPath = SUFFIX_NULL;
				} else {
					nextGUID = AssetDatabase.AssetPathToGUID(sanytizedPath);
					if (string.IsNullOrEmpty(nextGUID)) {
						this.nextPath = $"{sanytizedPath}{SUFFIX_NOT_FOUND}";
					} else {
						this.nextPath = sanytizedPath;
					}
				}
				CanSupport();
			}

			public bool IsValid() => !nextPath.EndsWith(SUFFIX_NOT_FOUND);

			public static string GetTransformPath(Transform tr) {
				// hoge/fuga/piyo
				System.Text.StringBuilder sb = new StringBuilder();
				for (var t = tr; null != t.parent; t = tr.parent) {
					sb.Insert(0, $"/{t.name}");
				}
				if (0 < sb.Length) {
					sb.Remove(0, 1);
				}

				return sb.ToString();
			}

			public bool Apply() {
				if (!IsValid()) {
					throw new System.InvalidOperationException();
				}
				if (!CanSupport()) {
					return false;
				}
				var prevValue = accessor.Get();
				var nextValue = GetNextValue();
				if (object.ReferenceEquals(prevValue, nextValue))
					return false;
				Debug.Log($"{Label}, changed {prevValue} to {nextValue}");
				accessor.Set(nextValue);
				return true;
			}

			bool CanSupport() {
				var currentValue = accessor.Get();
				if (null == currentValue) return true;
				var currentPath = CurrentPath;
				var currentValue2 = AssetDatabase.LoadAssetAtPath(currentPath, currentValue.GetType());
				return object.ReferenceEquals(currentValue, currentValue2);
			}

			UnityEngine.Object GetNextValue() {
				var prevValue = accessor.Get();
				var sanytizedPath = SanytizePath(nextPath);
				var targetType = (null == prevValue) ?
					typeof(UnityEngine.Object) :
					prevValue.GetType();
				var nextValue = string.IsNullOrEmpty(sanytizedPath) ?
					null :
					AssetDatabase.LoadAssetAtPath(sanytizedPath, targetType);
				if (null == nextValue)
					return null;

				if (null == prevValue)
					return nextValue;

				var nextGo = nextValue as GameObject;
				if (null != nextGo) {
					{
						var go = prevValue as GameObject;
						if (null != go) {
							var tr = go.transform;
							var trPath = GetTransformPath(tr);
							var nextTr = nextGo.transform.Find(trPath);
							if (null == nextTr)
								return null;
							return nextTr.gameObject;
						}
					}
					{
						var comp = prevValue as Component;
						if (null != comp) {
							var tr = comp.transform;
							var trPath = GetTransformPath(tr);
							var nextTr = nextGo.transform.Find(trPath);
							if (null == nextTr)
								return null;
							var nextComp = nextTr.gameObject.GetComponent(comp.GetType());
							if (null == nextComp)
								return null;
							return nextComp;
						}
					}
				}
				if (prevValue is UnityEditor.MonoScript) {
					return nextValue;
					//throw new System.NotSupportedException( $"{prevValue.GetType()}" );
				}
				if (prevValue.ToString() == " (UnityEngine.Prefab)") {
					Debug.Log($"{accessor.GetLabel()} not support, next: {nextValue.GetType()}");
					return prevValue;
					//	return nextValue;
				}
				return nextValue;
			}

			public static string GetPath(UnityEngine.Object unityObject) {
				if (null == unityObject)
					return "";
				AssetDatabase.TryGetGUIDAndLocalFileIdentifier(unityObject, out var guid, out long _);
				return AssetDatabase.GUIDToAssetPath(guid);
			}
		}
	}
}
