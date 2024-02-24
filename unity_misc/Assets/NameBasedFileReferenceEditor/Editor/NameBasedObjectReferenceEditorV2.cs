// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEditor;
// using System.Linq;
// using System.Text.RegularExpressions;
// using System.Text;
// using System;
// using System.Reflection;
// using NUnit.Compatibility;

// namespace Osakana4242 {
// 	public class NameBasedObjectReferenceEditorV2 : EditorWindow {

// 		string rightText_ = "";
// 		File[] workList_ = new File[0];

// 		[MenuItem("Window/Osakana4242/NameBasedObjectReferenceEditorV2")]
// 		static void Init() {
// 			EditorWindow.GetWindow<NameBasedObjectReferenceEditorV2>();
// 		}

// 		class File {
// 			public readonly string guid;
// 			public readonly string path;
// 			readonly TargetField[] fieldArr_;
// 			readonly UnityEngine.Object object_;

// 			static void LoadObject(object inst, List<TargetField> outFieldList, List<object> depthList) {
// 				if (depthList.Count == 0) {
// 					var root = (UnityEngine.Object)inst;
// 					Debug.Log($"root: {root.name}");
// 				}

// 				if (127 < depthList.Count)
// 					throw new System.Exception();
// 				if (depthList.Contains(inst))
// 					return;
// 				try {
// 					depthList.Add(inst);

// 					var list = inst as IList;
// 					if (null != list) {
// 						for (int i = 0; i < list.Count; ++i) {
// 							var item = list[i];
// 							LoadObject(item, outFieldList, depthList);
// 						}
// 					} else {
// 						var instType = inst.GetType();
// 						var fields = instType.GetFields(
// 							System.Reflection.BindingFlags.Instance |
// 							System.Reflection.BindingFlags.NonPublic |
// 							System.Reflection.BindingFlags.Public
// 						);

// 						for (int i = 0; i < fields.Length; ++i) {
// 							var field = fields[i];
// 							LoadField(inst, field, outFieldList, depthList);
// 						}
// 					}
// 				} finally {
// 					depthList.RemoveAt(depthList.Count - 1);
// 				}
// 			}

// 			static void LoadField(object owner, FieldInfo field, List<TargetField> outFieldList, List<object> depthList) {
// 				var fieldType = field.FieldType;
// 				if (!IsSerializable(field))
// 					return;
// 				if (fieldType.IsValueType)
// 					return;

// 				if (TargetField.CanCreate(fieldType)) {
// 					outFieldList.AddRange(TargetField.Creates(owner, field));
// 				} else {
// 					var value = field.GetValue(owner);
// 					LoadObject(value, outFieldList, depthList);
// 				}
// 			}

// 			static bool IsSerializable(FieldInfo field) {
// 				var fieldType = field.FieldType;
// 				if (field.IsDefined(typeof(System.NonSerializedAttribute)))
// 					return false;
// 				if (!field.IsPublic && !field.IsDefined(typeof(UnityEngine.SerializeField)))
// 					return false;

// 				if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType)) {
// 					return true;
// 				} else {
// 					if (!fieldType.IsDefined(typeof(System.SerializableAttribute)))
// 						return false;
// 					return true;
// 				}
// 			}


// 			public File(string guid, string path) {
// 				this.guid = guid;
// 				this.path = path;
// 				object_ = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

// 				var fieldList = new List<TargetField>();
// 				LoadObject(object_, fieldList, new List<object>());
// 				fieldArr_ = fieldList.ToArray();
// 			}

// 			File(File other, TargetField[] itemArr) {
// 				this.guid = other.guid;
// 				this.path = other.path;
// 				this.object_ = other.object_;
// 				this.fieldArr_ = itemArr;
// 			}

// 			public bool CanApply() {
// 				var invalidIndex = System.Array.FindIndex(fieldArr_, (_item) => !_item.IsValid());
// 				return invalidIndex == -1;
// 			}

// 			public void Apply() {
// 				if (!CanApply()) {
// 					throw new System.InvalidOperationException("Can't Apply");
// 				}
// 				var isDirty = false;
// 				for (int i = 0; i < fieldArr_.Length; ++i) {
// 					var field = fieldArr_[i];
// 					isDirty |= field.Apply();
// 				}
// 				if (isDirty) {
// 					EditorUtility.SetDirty(object_);
// 					AssetDatabase.SaveAssetIfDirty(object_);
// 				}
// 			}

// 			public int LineCount => fieldArr_.Length;

// 			public string[] ToLines() {
// 				var lineList = new List<string>();
// 				foreach (var item in fieldArr_) {
// 					lineList.Add($"{path}\t{item.label}\t{item.nextPath}");
// 				}
// 				return lineList.ToArray();
// 			}

// 			public void Write(StringBuilder sb) {
// 				foreach (var line in ToLines()) {
// 					sb.Append($"{line}\n");
// 				}
// 			}

// 			public File Read(Span<string> lines) {
// 				var fieldList2 = new List<TargetField>(fieldArr_.Length);
// 				for (int i = 0; i < lines.Length; ++i) {
// 					var line = lines[i];
// 					if (line.StartsWith('#'))
// 						continue;
// 					var cols = line.Split("\t");
// 					var ownerFileName = cols[0];
// 					var label = cols[1];
// 					var fileName = cols[2];
// 					var field1 = fieldArr_[fieldList2.Count];
// 					if(field1.label != label)
// 						throw new System.ArgumentException("テキストのずれを検出");
// 					var field2 = new TargetField(field1.owner, field1.info, field1.listIndex, fileName);
// 					fieldList2.Add(field2);
// 				}
// 				return new File(this, fieldList2.ToArray());
// 			}


// 		}

// 		class TargetField {
// 			const string suffix = ":(Not Found)";
// 			public readonly string path;
// 			public readonly string nextPath;
// 			public readonly string nextGUID;
// 			public readonly object owner;
// 			public readonly FieldInfo info;
// 			public readonly int listIndex;
// 			public readonly string label;

// 			public static bool CanCreate(System.Type type) {
// 				if (typeof(UnityEngine.Object).IsAssignableFrom(type))
// 					return true;
// 				if (IsUnityObjectList(type))
// 					return true;
// 				return false;
// 			}

// 			static bool IsUnityObjectList(System.Type type) {
// 				System.Type elementType;
// 				if (type.IsArray) {
// 					elementType = type.GetElementType();
// 				} else {
// 					if (!type.IsGenericType)
// 						return false;
// 					if (!typeof(IList<>).IsAssignableFrom(type.GetGenericTypeDefinition()))
// 						return false;
// 					elementType = type.GenericTypeArguments[0];
// 				}

// 				if (!typeof(UnityEngine.Object).IsAssignableFrom(elementType))
// 					return false;
// 				return true;
// 			}

// 			public static TargetField[] Creates(object owner, FieldInfo field) {
// 				if (IsUnityObjectList(field.FieldType)) {
// 					var valueOrList = field.GetValue(owner);
// 					var list = (IList)valueOrList;
// 					var fieldList = new List<TargetField>();
// 					for (int i = 0; i < list.Count; ++i) {
// 						var item = Create(owner, field, i);
// 						fieldList.Add(item);
// 					}
// 					return fieldList.ToArray();
// 				} else {
// 					var item = Create(owner, field, -1);
// 					return new TargetField[] { item };
// 				}
// 			}

// 			public static UnityEngine.Object Get(object owner, FieldInfo field, int index) {
// 				var valueOrList = field.GetValue(owner);
// 				if (index == -1) {
// 					return (UnityEngine.Object)valueOrList;
// 				} else {
// 					var list = (IList)valueOrList;
// 					return (UnityEngine.Object)list[index];
// 				}
// 			}

// 			public static void Set(object owner, FieldInfo field, int index, UnityEngine.Object value) {
// 				if (index == -1) {
// 					field.SetValue(owner, value);
// 				} else {
// 					var valueOrList = field.GetValue(owner);
// 					var list = (IList)valueOrList;
// 					list[index] = value;
// 				}
// 			}

// 			public static TargetField Create(object owner, FieldInfo field, int index) {
// 				var unityObject = Get(owner, field, index);
// 				var path = GetPath(unityObject);
// 				return new TargetField(owner, field, index, path);
// 			}

// 			public static string GetPath(object owner, FieldInfo info) {
// 				var unityObject = (UnityEngine.Object)info.GetValue(owner);
// 				return GetPath(unityObject);
// 			}

// 			public static string GetPath(UnityEngine.Object unityObject) {
// 				if (null == unityObject)
// 					return "";
// 				AssetDatabase.TryGetGUIDAndLocalFileIdentifier(unityObject, out var guid, out long _);
// 				return AssetDatabase.GUIDToAssetPath(guid);
// 			}

// 			public TargetField(object owner, FieldInfo field, int listIndex, string nextPath) {
// 				this.owner = owner;
// 				this.info = field;
// 				this.listIndex = listIndex;
// 				this.label = listIndex == -1 ?
// 					$"{field.Name}" :
// 					$"{field.Name}[{listIndex}]";
// 				if (string.IsNullOrEmpty(nextPath)) {
// 					this.nextPath = "";
// 				} else {
// 					string sanytizedPath;
// 					if (nextPath.EndsWith(suffix)) {
// 						sanytizedPath = nextPath.Substring(0, nextPath.Length - suffix.Length);
// 					} else {
// 						sanytizedPath = nextPath;
// 					}
// 					nextGUID = AssetDatabase.AssetPathToGUID(sanytizedPath);
// 					if (string.IsNullOrEmpty(nextGUID)) {
// 						this.nextPath = $"{sanytizedPath}{suffix}";
// 					} else {
// 						this.nextPath = sanytizedPath;
// 					}
// 				}
// 			}

// 			public bool IsValid() => !nextPath.EndsWith(suffix);

// 			public bool Apply() {
// 				if (!IsValid())
// 					throw new System.InvalidOperationException();
// 				var prevValue = info.GetValue(owner);
// 				var nextValue = string.IsNullOrEmpty(nextPath) ?
// 					null :
// 					AssetDatabase.LoadAssetAtPath(nextPath, info.FieldType);
// 				if (object.ReferenceEquals(prevValue, nextValue))
// 					return false;
// 				info.SetValue(owner, nextValue);
// 				return true;
// 			}
// 		}

// 		void LoadFromSelection() {
// 			GUI.FocusControl(null);
// 			AssetDatabase.Refresh();

// 			try {
// 				var list = Selection.objects.
// 					Select(_item => {
// 						var isValid = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(_item.GetInstanceID(), out var guid, out long _);
// 						return (isValid, guid);
// 					}).
// 					Where(_item => _item.isValid).
// 					Select(_item => (_item.guid, path: AssetDatabase.GUIDToAssetPath(_item.guid)));

// 				var workList2 = new List<File>();
// 				// load	
// 				foreach (var item in list) {
// 					ThrowIfCanceled();
// 					var item2 = new File(item.guid, item.path);
// 					workList2.Add(item2);
// 				}
// 				this.workList_ = workList2.ToArray();
// 				BuildText();
// 			} catch (System.OperationCanceledException) {
// 				EditorUtility.DisplayDialog("Canceled", "Canceled", "OK");
// 			} catch (System.Exception ex) {
// 				Debug.LogException(ex);
// 			} finally {
// 				EditorUtility.ClearProgressBar();
// 			}
// 			GUI.FocusControl(null);
// 		}

// 		void BuildText() {
// 			var sb = new System.Text.StringBuilder();
// 			foreach (var file in workList_) {
// 				ThrowIfCanceled();
// 				file.Write(sb);
// 			}
// 			rightText_ = sb.ToString();
// 		}

// 		void ThrowIfCanceled() {
// 			var canceled = EditorUtility.DisplayCancelableProgressBar("title", "body", 0);
// 			if (!canceled) return;
// 			throw new System.OperationCanceledException();
// 		}

// 		void Apply() {
// 			GUI.FocusControl(null);
// 			try {
// 				var lines = rightText_.Split('\n').AsSpan();
// 				var index = 0;

// 				var nextWorkList = new File[workList_.Length];
// 				for (int i = 0; i < workList_.Length; ++i) {
// 					ThrowIfCanceled();
// 					var item = workList_[i];
// 					var span = lines.Slice(index, item.LineCount);
// 					nextWorkList[i] = item.Read(span);
// 					index += item.LineCount;
// 				}

// 				workList_ = nextWorkList;
// 				BuildText();

// 				var canApply = System.Array.FindIndex(workList_, _item => !_item.CanApply()) == -1;
// 				if (!canApply)
// 					return;

// 				try {
// 					for (int i = 0; i < workList_.Length; ++i) {
// 						ThrowIfCanceled();
// 						var item = workList_[i];
// 						item.Apply();
// 					}
// 				} finally {
// 					AssetDatabase.Refresh();
// 				}
// 			} finally {
// 				EditorUtility.ClearProgressBar();
// 			}
// 		}

// 		void OnGUI() {
// 			if (GUILayout.Button("Load")) {
// 				LoadFromSelection();
// 			}
// 			if (GUILayout.Button("Apply")) {
// 				Apply();
// 			}
// 			rightText_ = EditorGUILayout.TextArea(rightText_);

// 		}
// 	}
// }
