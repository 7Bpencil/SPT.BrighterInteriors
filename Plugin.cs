//
// Copyright (c) 2026 7Bpencil
//
// This source code is licensed under the MIT license found in the
// LICENSE file in the root directory of this source tree.
//

using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Configuration;
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using SPTushonka.Reflection.Patching;

namespace SevenBoldPencil.BrighterInteriors
{
    [BepInPlugin("7Bpencil.BrighterInteriors", "7Bpencil.BrighterInteriors", "1.1.1")]
    public class Plugin : BasePlugin
	{
		public static ConfigEntry<float> ShadowOpacity;

		public override void Load()
		{
			ShadowOpacity = Config.Bind<float>("Main", "Shadow Opacity", 0.7f, new ConfigDescription("0 is disabled, 1 is original", new AcceptableValueRange<float>(0f, 1f)));
			new Patch_AmbientLight_DrawStencilShadow().Enable();
        }
    }

    public class Patch_AmbientLight_DrawStencilShadow : ModulePatch
    {
		public const string StencilShadowsUnitMarkerName = "StencilShadows_unit"; // same as AmbientLight._stencilShadowsMarkerName + "_unit"
        public static readonly int _StencilAmbientColor = Shader.PropertyToID("_StencilAmbientColor");
        public static readonly int _StencilFogAttenuation = Shader.PropertyToID("_StencilFogAttenuation");

        protected override MethodBase GetTargetMethod()
        {
			Type[] parameters = [typeof(CommandBuffer), typeof(StencilShadow), typeof(Vector3), typeof(bool)];
            return AccessTools.Method(typeof(AmbientLight), nameof(AmbientLight.DrawStencilShadow), parameters);
        }

        [PatchPrefix]
        public static bool Prefix(AmbientLight __instance, ref bool __result, CommandBuffer cmdBuf, StencilShadow ss, Vector3 camPos, bool disableColorPass = false)
        {
			if (!ss.Culling.PassCulling((ss.Bounds.center - camPos).sqrMagnitude, out var num))
			{
				__result = false;
				return false;
			}

			var _clearStencilMaterial = __instance._clearStencilMaterial;
			var _writeStencilMaterial = __instance._writeStencilMaterial;
			var _quadMesh = AmbientLight._quadMesh;

			cmdBuf.BeginSample(StencilShadowsUnitMarkerName);
			cmdBuf.DrawMesh(_quadMesh, Matrix4x4.identity, _clearStencilMaterial);
			cmdBuf.DrawRenderer(ss.Renderer, _writeStencilMaterial, 0, 0);
			cmdBuf.DrawRenderer(ss.Renderer, _writeStencilMaterial, 0, 1);
			if (!disableColorPass)
			{
				cmdBuf.SetGlobalColor(_StencilAmbientColor, ss.Ambient * num * Plugin.ShadowOpacity.Value);
				cmdBuf.SetGlobalFloat(_StencilFogAttenuation, ss.FogAttenuation);
				cmdBuf.DrawMesh(_quadMesh, Matrix4x4.identity, _writeStencilMaterial, 0, 2);
			}
			cmdBuf.DrawRenderer(ss.Renderer, _writeStencilMaterial, 0, 3);
			cmdBuf.DrawRenderer(ss.Renderer, _writeStencilMaterial, 0, 4);
			cmdBuf.EndSample(StencilShadowsUnitMarkerName);

			__result = true;
			return false;
        }
    }
}
