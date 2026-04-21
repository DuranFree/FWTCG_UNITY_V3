using NUnit.Framework;
using UnityEngine;
using FWTCG.UI;

namespace FWTCG.Tests
{
    /// <summary>
    /// 验证 SpellDissolveFX 工具类：材质创建、方向翻转、Amount 重置。
    /// 不覆盖粒子爆发（需要 Canvas/RectTransform 运行时环境，属 PlayMode 测试范畴）。
    /// </summary>
    [TestFixture]
    public class SpellDissolveFXTests
    {
        private Material _mat;

        [SetUp]
        public void SetUp()
        {
            _mat = SpellDissolveFX.CreateDissolveMaterial();
        }

        [TearDown]
        public void TearDown()
        {
            if (_mat != null) Object.DestroyImmediate(_mat);
            _mat = null;
        }

        [Test]
        public void CreateDissolveMaterial_ReturnsNonNull_WhenShaderAvailable()
        {
            // Shader 'FWTCG/UIDissolve' 应当被项目包含；若 CI 剥离 shader 会返回 null
            if (Shader.Find(SpellDissolveFX.SHADER_NAME) == null)
                Assert.Ignore("UIDissolve shader stripped; skipping.");
            Assert.IsNotNull(_mat);
            Assert.AreEqual(SpellDissolveFX.SHADER_NAME, _mat.shader.name);
        }

        [Test]
        public void CreateDissolveMaterial_InitialAmount_IsZero()
        {
            if (_mat == null) Assert.Ignore("UIDissolve shader stripped; skipping.");
            Assert.AreEqual(0f, _mat.GetFloat("_DissolveAmount"));
        }

        [Test]
        public void SetDirection_Player_Upward()
        {
            if (_mat == null) Assert.Ignore("UIDissolve shader stripped; skipping.");
            SpellDissolveFX.SetDirection(_mat, fromPlayer: true);
            var dir = _mat.GetVector("_DissolveDirection");
            Assert.AreEqual(0f, dir.x, 0.001f);
            Assert.AreEqual(1f, dir.y, 0.001f, "Player dissolve must sweep bottom→top (y=+1)");
        }

        [Test]
        public void SetDirection_Opponent_Downward()
        {
            if (_mat == null) Assert.Ignore("UIDissolve shader stripped; skipping.");
            SpellDissolveFX.SetDirection(_mat, fromPlayer: false);
            var dir = _mat.GetVector("_DissolveDirection");
            Assert.AreEqual(0f, dir.x, 0.001f);
            Assert.AreEqual(-1f, dir.y, 0.001f, "Opponent dissolve must sweep top→bottom (y=-1)");
        }

        [Test]
        public void ResetAmount_ResetsToZero()
        {
            if (_mat == null) Assert.Ignore("UIDissolve shader stripped; skipping.");
            _mat.SetFloat("_DissolveAmount", 0.73f);
            SpellDissolveFX.ResetAmount(_mat);
            Assert.AreEqual(0f, _mat.GetFloat("_DissolveAmount"));
        }

        [Test]
        public void SetDirection_NullMaterial_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => SpellDissolveFX.SetDirection(null, true));
            Assert.DoesNotThrow(() => SpellDissolveFX.ResetAmount(null));
            Assert.DoesNotThrow(() => SpellDissolveFX.TweenAmount(null, 1f, 0.5f));
        }
    }
}
