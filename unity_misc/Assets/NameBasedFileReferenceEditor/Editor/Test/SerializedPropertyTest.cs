using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

namespace Osakana4242.Content.Inners.Tests {
	public class SerializedPropertyTest {
		[Test]
		public void TransformFindTest() {
			var goA = new GameObject("a");
			var goB = new GameObject("b");
			var goC = new GameObject("c");
			try {
				// a/b/c
				var trA = goA.transform;
				var trB = goB.transform;
				var trC = goC.transform;
				trB.transform.parent = trA;
				trC.transform.parent = trB;
				{
					var tr = trA.Find("");
					Assert.AreEqual( trA, tr);
				}
				{
					var tr = trA.Find("b");
					Assert.AreEqual( trB, tr);
				}
				{
					var tr = trA.Find("b/c");
					Assert.AreEqual( trC, tr);
				}
				{
					var tr = trB.Find("..");
					Assert.AreEqual( trA, tr);
				}
				{
					var tr = trC.Find("../b");
					Assert.AreEqual( trB, tr);
				}
				{
					var tr = trC.Find("../..");
					Assert.AreEqual( trA, tr);
				}
				{
					var tr = trA.Find(".");
					Assert.AreEqual( null, tr);
				}
				{
					var tr = trA.Find("./b");
					Assert.AreEqual( null, tr);
				}
			} finally {
				UnityEngine.Object.DestroyImmediate(goA);
				UnityEngine.Object.DestroyImmediate(goB);
			}
		}

		[Test]
		public void Hoge() {


			var cls1_i1 = ScriptableObject.CreateInstance<Cls1>();
			var cls1_i2 = ScriptableObject.CreateInstance<Cls1>();
			cls1_i1.field1 = new Cls2() {
				field21 = cls1_i2,
				field22 = cls1_i2,
			};

			var so = new SerializedObject(cls1_i1);
			// {
			// 	var sp = so.FindProperty("field1.field21");
			// 	sp.objectReferenceValue = null;
			// }

			{
				string targetPropertyPath = "";
				SerializedProperty targetSp = default;
				for (var sp = so.GetIterator(); sp.Next(true);) {
					Debug.Log($"type: {sp.GetPropertyType()}");
					if (sp.propertyPath == "field1.field22") {
						targetSp = sp;
						targetPropertyPath = sp.propertyPath;
					}
				}
				so.FindProperty(targetPropertyPath).objectReferenceValue = null;
				//				targetSp.objectReferenceValue = null;
			}
		}

		class Cls1 : ScriptableObject {
			public Cls2 field1;
		}

		[System.Serializable]
		class Cls2 {
			public Cls1 field21;
			public Cls1 field22;
		}
	}


	public static class SerializedPropertyUtility {
		/// <summary>
		/// SerializedProperty から FieldInfo を取得する
		/// </summary>
		public static FieldInfo GetFieldInfo(this SerializedProperty property) {
			FieldInfo GetField(Type type, string path) {
				return type.GetField(path, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
			}

			var parentType = property.serializedObject.targetObject.GetType();
			var splits = property.propertyPath.Split('.');
			var fieldInfo = GetField(parentType, splits[0]);
			for (var i = 1; i < splits.Length; i++) {
				if (splits[i] == "Array") {
					i += 2;
					if (i >= splits.Length)
						continue;

					var type = fieldInfo.FieldType.IsArray
						? fieldInfo.FieldType.GetElementType()
						: fieldInfo.FieldType.GetGenericArguments()[0];

					fieldInfo = GetField(type, splits[i]);
				} else {
					fieldInfo = i + 1 < splits.Length && splits[i + 1] == "Array"
						? GetField(parentType, splits[i])
						: GetField(fieldInfo.FieldType, splits[i]);
				}

				if (fieldInfo == null)
					throw new Exception("Invalid FieldInfo. " + property.propertyPath);

				parentType = fieldInfo.FieldType;
			}

			return fieldInfo;
		}

		/// <summary>
		/// SerializedProperty から Field の Type を取得する 
		/// </summary>
		/// <param name="property">SerializedProperty</param>
		/// <param name="isArrayListType">array や List の場合要素の Type を取得するか</param>
		public static Type GetPropertyType(this SerializedProperty property, bool isArrayListType = false) {
			var fieldInfo = property.GetFieldInfo();

			// 配列の場合は配列のTypeを返す
			if (isArrayListType && property.isArray && property.propertyType != SerializedPropertyType.String)
				return fieldInfo.FieldType.IsArray
					? fieldInfo.FieldType.GetElementType()
					: fieldInfo.FieldType.GetGenericArguments()[0];
			return fieldInfo.FieldType;
		}
	}
}
