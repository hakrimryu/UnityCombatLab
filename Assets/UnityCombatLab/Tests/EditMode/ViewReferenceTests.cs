using System;
using NUnit.Framework;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.UI;
using UnityCombatLab.Presentation.Units;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// 非アクティブな prefab 複製で、必須参照の欠落と初期化前の解放を確認します。
    /// [KO] 비활성 prefab 복제에서 필수 참조 누락과 초기화 전 해제를 확인합니다.
    /// [EN] Checks missing required references and cleanup before initialization on inactive prefab copies.
    /// </summary>
    public sealed class ViewReferenceTests
    {
        /// <summary>
        /// ボタン・配列長・配列要素の不備を、入力接続前に原因のフィールド名で報告します。
        /// [KO] 버튼·배열 길이·배열 원소 오류를 입력 연결 전에 원인 필드명으로 보고합니다.
        /// [EN] Reports missing buttons, incorrect array lengths and missing entries by field name before input setup.
        /// </summary>
        [TestCase("_titleStart")]
        [TestCase("_heroButtons")]
        [TestCase("_slotTexts.Array.data[1]")]
        public void FrontendRejectsIncompleteReferences(string propertyPath)
        {
            var owner = new GameObject("InactiveFrontendTest");
            owner.SetActive(false);
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/UnityCombatLab/Prefabs/UI/Frontend/UI_Frontend.prefab");
                var view = Object.Instantiate(prefab, owner.transform).GetComponent<FrontendView>();
                using var serialized = new SerializedObject(view);
                var property = serialized.FindProperty(propertyPath);
                if (property.isArray)
                    property.arraySize = 1;
                else property.objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var exception = Assert.Throws<InvalidOperationException>(() => view.Initialize());
                Assert.That(exception.Message, Does.Contain(propertyPath.Split('.')[0]));
                Assert.That(owner.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>
        /// 回転表示が未設定なら、ローディングを有効化する前に失敗を報告します。
        /// [KO] 회전 표시가 없으면 로딩을 활성화하기 전에 실패를 보고합니다.
        /// [EN] Reports a missing spinner before activating the loading display.
        /// </summary>
        [Test]
        public void LoadingRejectsMissingSpinnerBeforeActivation()
        {
            var owner = new GameObject("InactiveLoadingTest");
            owner.SetActive(false);
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/UnityCombatLab/Prefabs/UI/Common/UI_LoadingCanvas.prefab");
                var view = Object.Instantiate(prefab, owner.transform).GetComponent<LoadingView>();
                view.gameObject.SetActive(false);
                using var serialized = new SerializedObject(view);
                serialized.FindProperty("_spinner").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var exception = Assert.Throws<InvalidOperationException>(
                    () => view.Show(Define.RunTransitionPhase.LoadingDungeon));
                Assert.That(exception.Message, Does.Contain("spinner"));
                Assert.That(view.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>
        /// 元の姿勢を保存する前に必須の移動軸が欠けても、解放で例外やモデルの変形を起こしません。
        /// [KO] 원래 자세를 저장하기 전에 필수 이동 축이 빠져도 해제 시 예외나 모델 변형을 일으키지 않습니다.
        /// [EN] Avoids cleanup exceptions and model deformation if the required motion pivot is absent before pose capture.
        /// </summary>
        [Test]
        public void UnitCleanupBeforeInitializationPreservesAuthoredPose()
        {
            var owner = new GameObject("InactiveUnitTest");
            owner.SetActive(false);
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/UnityCombatLab/Prefabs/Units/Heroes/Unit_Knight.prefab");
                var view = Object.Instantiate(prefab, owner.transform).GetComponent<UnitView>();
                using var serialized = new SerializedObject(view);
                var model = (Transform)serialized.FindProperty("_model").objectReferenceValue;
                var scale = model.localScale;
                var rotation = model.localRotation;
                serialized.FindProperty("_motionRoot").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.DoesNotThrow(view.Release);
                Assert.That(model.localScale, Is.EqualTo(scale));
                Assert.That(model.localRotation, Is.EqualTo(rotation));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
