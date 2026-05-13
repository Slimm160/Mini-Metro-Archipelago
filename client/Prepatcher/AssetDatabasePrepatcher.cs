using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace client.Prepatcher;

/// <summary>
/// BepInEx 5 prepatcher that injects a <c>RemoveAsset(AssetType, bool)</c> instance method
/// into <c>Metro.AssetDatabase</c>. The vanilla class has no public way to decrement the
/// private <c>totalAssets</c>/<c>availableAssets</c>/<c>initialAssets</c> bookkeeping —
/// only <c>ConsumeAsset</c>, which decrements <c>availableAssets</c> alone. Permanent
/// deletion (see <c>GameApi.Take.DeletePermanently</c>) needs to roll back all three
/// arrays in lockstep with <c>AddAsset</c>, so we inject the inverse here.
/// </summary>
public static class AssetDatabasePrepatcher
{
    private static readonly ManualLogSource Log =
        Logger.CreateLogSource("client.Prepatcher");

    public static IEnumerable<string> TargetDLLs => new[] { "Assembly-CSharp.dll" };

    public static void Patch(AssemblyDefinition assembly)
    {
        var module = assembly.MainModule;

        var assetDb = module.GetType("Metro.AssetDatabase");
        if (assetDb == null)
        {
            Log.LogWarning("Metro.AssetDatabase not found — skipping RemoveAsset injection.");
            return;
        }

        // Idempotent — never inject twice if the patcher somehow runs against an already-patched assembly.
        if (assetDb.Methods.Any(m => m.Name == "RemoveAsset"))
        {
            Log.LogInfo("RemoveAsset already present — skipping.");
            return;
        }

        var gameField    = assetDb.Fields.FirstOrDefault(f => f.Name == "game");
        var initialField = assetDb.Fields.FirstOrDefault(f => f.Name == "initialAssets");
        var totalField   = assetDb.Fields.FirstOrDefault(f => f.Name == "totalAssets");
        var availField   = assetDb.Fields.FirstOrDefault(f => f.Name == "availableAssets");
        if (gameField == null || initialField == null || totalField == null || availField == null)
        {
            Log.LogWarning("AssetDatabase fields missing — game compiled with different layout? Skipping.");
            return;
        }

        var gameType = gameField.FieldType.Resolve();
        var hudScreenGetter = gameType?.Properties.FirstOrDefault(p => p.Name == "HudScreen")?.GetMethod;
        var hudScreenType = hudScreenGetter?.ReturnType.Resolve();
        var assetPanelGetter = hudScreenType?.Properties.FirstOrDefault(p => p.Name == "AssetPanel")?.GetMethod;
        var assetPanelType = assetPanelGetter?.ReturnType.Resolve();
        var updateAssetMethod = assetPanelType?.Methods.FirstOrDefault(m =>
            m.Name == "UpdateAsset" && m.Parameters.Count == 2);
        if (hudScreenGetter == null || assetPanelGetter == null || updateAssetMethod == null)
        {
            Log.LogWarning("HUD lookup chain (Game.HudScreen.AssetPanel.UpdateAsset) not found — skipping.");
            return;
        }

        // AssetType param type is shared with AddAsset's first parameter.
        var addAsset = assetDb.Methods.FirstOrDefault(m => m.Name == "AddAsset" && m.Parameters.Count == 2);
        if (addAsset == null)
        {
            Log.LogWarning("AssetDatabase.AddAsset(AssetType, bool) not found — skipping.");
            return;
        }
        var assetTypeRef = addAsset.Parameters[0].ParameterType;

        var method = new MethodDefinition(
            "RemoveAsset",
            MethodAttributes.Public | MethodAttributes.HideBySig,
            module.TypeSystem.Void);

        method.Parameters.Add(new ParameterDefinition("type", ParameterAttributes.None, assetTypeRef));
        var isInitialParam = new ParameterDefinition(
            "isInitial",
            ParameterAttributes.HasDefault | ParameterAttributes.Optional,
            module.TypeSystem.Boolean)
        {
            Constant = false,
        };
        method.Parameters.Add(isInitialParam);

        var il = method.Body.GetILProcessor();

        // Branch targets — emitted as Nops we'll Append later so Brfalse_S can point to them.
        var skipInitial = Instruction.Create(OpCodes.Nop);
        var skipHud     = Instruction.Create(OpCodes.Nop);

        // if (!isInitial) goto skipInitial;
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Brfalse_S, skipInitial);

        // initialAssets[(int)type]--;
        EmitArrayDecrement(il, module, initialField);

        // skipInitial:
        il.Append(skipInitial);

        // totalAssets[(int)type]--;
        EmitArrayDecrement(il, module, totalField);

        // availableAssets[(int)type]--;
        EmitArrayDecrement(il, module, availField);

        // if (game.HudScreen == null) goto skipHud;
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, gameField);
        il.Emit(OpCodes.Callvirt, hudScreenGetter);
        il.Emit(OpCodes.Brfalse_S, skipHud);

        // game.HudScreen.AssetPanel.UpdateAsset(type, false);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, gameField);
        il.Emit(OpCodes.Callvirt, hudScreenGetter);
        il.Emit(OpCodes.Callvirt, assetPanelGetter);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Callvirt, updateAssetMethod);

        // skipHud:
        il.Append(skipHud);

        il.Emit(OpCodes.Ret);

        assetDb.Methods.Add(method);

        Log.LogInfo("Injected Metro.AssetDatabase.RemoveAsset(AssetType, bool).");
    }

    /// <summary>Emit IL for <c>field[(int)arg1]--;</c> where <c>field</c> is an int[].</summary>
    private static void EmitArrayDecrement(ILProcessor il, ModuleDefinition module, FieldDefinition field)
    {
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, field);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldelema, module.TypeSystem.Int32);
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Ldind_I4);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Sub);
        il.Emit(OpCodes.Stind_I4);
    }
}
