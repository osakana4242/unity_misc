using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

namespace Sandbox.NameBasedReferenceEditor.Test {
	public class TransformTest {
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
					Assert.AreEqual(trA, tr);
				}
				{
					var tr = trA.Find("b");
					Assert.AreEqual(trB, tr);
				}
				{
					var tr = trA.Find("b/c");
					Assert.AreEqual(trC, tr);
				}
				{
					var tr = trB.Find("..");
					Assert.AreEqual(trA, tr);
				}
				{
					var tr = trC.Find("../b");
					Assert.AreEqual(trB, tr);
				}
				{
					var tr = trC.Find("../..");
					Assert.AreEqual(trA, tr);
				}
				{
					var tr = trA.Find(".");
					Assert.AreEqual(null, tr);
				}
				{
					var tr = trA.Find("./b");
					Assert.AreEqual(null, tr);
				}
			} finally {
				UnityEngine.Object.DestroyImmediate(goA);
				UnityEngine.Object.DestroyImmediate(goB);
			}
		}
	}
}
