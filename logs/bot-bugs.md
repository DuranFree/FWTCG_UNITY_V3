# Bot 实时 Bug 日志

- 启动时间: 2026-04-24 09:47:48
- 速度倍数: 5x
- 目标局数: 20

---

## 🔵 [Low][Warning] 第1局 (09:47:53)

- 状态: Phase=end Round=3 Turn=player
- 策略: Greedy
- Seed: 185190508
- 详情:

```
<color=#0099bc><b>DOTWEEN ► </b></color>Target or field is missing/null () ► The object of type 'Material' has been destroyed but you are still trying to access it.
Your script should either check if it is null or you should not destroy the object.

  at (wrapper managed-to-native) UnityEngine.Material.SetFloatImpl(UnityEngine.Material,int,single)
  at UnityEngine.Material.SetFloat (System.String name, System.Single value) [0x00008] in <8cdc7e7e8a17404287a8b220eb9eec72>:0 
  at DG.Tweening.ShortcutExtensions+<>c__DisplayClass22_0.<DOFloat>b__1 (System.Single x) [0x00000] in D:\DG\_Develop\__UNITY_ASSETS\_Demigiant\__DOTween\_DOTween.Assembly\DOTween\ShortcutExtensions.cs:337 
  at DG.Tweening.Plugins.FloatPlugin.EvaluateAndApply (DG.Tweening.Plugins.Options.FloatOptions options, DG.Tweening.Tween t, System.Boolean isRelative, DG.Tweening.Core.DOGetter`1[T] getter, DG.Tweening.Core.DOSetter`1[T] setter, System.Single elapsed, System.Single startValue, System.Single changeValue, System.Single duration, System.Boolean usingInversePosition, System.Int32 newCompletedSteps, DG.Tweening.Core.Enums.UpdateNotice updateNotice) [0x00084] in D:\DG\_Develop\__UNITY_ASSETS\_Demigiant\__DOTween\_DOTween.Assembly\DOTween\Plugins\FloatPlugin.cs:73 
  at DG.Tweening.Core.TweenerCore`3[T1,T2,TPlugOptions].ApplyTween (System.Single prevPosition, System.Int32 prevCompletedLoops, System.Int32 newCompletedSteps, System.Boolean useInversePosition, DG.Tweening.Core.Enums.UpdateMode updateMode, DG.Tweening.Core.Enums.UpdateNotice updateNotice) [0x00030] in D:\DG\_Develop\__UNITY_ASSETS\_Demigiant\__DOTween\_DOTween.Assembly\DOTween\Core\TweenerCore.cs:261 


UnityEngine.Debug:LogWarning (object)
DG.Tweening.Core.Debugger:LogSafeModeCapturedError (object,DG.Tweening.Tween) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Core/Debugger.cs:61)
DG.Tweening.Core.TweenerCore`3<single, single, DG.Tweening.Plugins.Options.FloatOptions>:ApplyTween (single,int,int,bool,DG.Tweening.Core.Enums.UpdateMode,DG.Tweening.Core.Enums.UpdateNotice) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Core/TweenerCore.cs:265)
DG.Tweening.Tween:DoGoto (DG.Tweening.Tween,single,int,DG.Tweening.Core.Enums.UpdateMode) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Tween.cs:266)
DG.Tweening.Core.TweenManager:Goto (DG.Tweening.Tween,single,bool,DG.Tweening.Core.Enums.UpdateMode) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Core/TweenManager.cs:803)
DG.Tweening.Sequence:ApplyInternalCycle (DG.Tweening.Sequence,single,single,DG.Tweening.Core.Enums.UpdateMode,bool,bool,bool) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Sequence.cs:369)
DG.Tweening.Sequence:DoApplyTween (DG.Tweening.Sequence,single,int,int,bool,DG.Tweening.Core.Enums.UpdateMode) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Sequence.cs:275)
DG.Tweening.Sequence:ApplyTween (single,int,int,bool,DG.Tweening.Core.Enums.UpdateMode,DG.Tweening.Core.Enums.UpdateNotice) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Sequence.cs:166)
DG.Tweening.Tween:DoGoto (DG.Tweening.Tween,single,int,DG.Tweening.Core.Enums.UpdateMode) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Tween.cs:266)
DG.Tweening.Core.TweenManager:Update (DG.Tweening.Tween,single,single,bool) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Core/TweenManager.cs:596)
DG.Tweening.Core.TweenManager:Update (DG.Tweening.UpdateType,single,single) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Core/TweenManager.cs:444)
DG.Tweening.Core.DOTweenComponent:Update () (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Core/DOTweenComponent.cs:75)

```

## 🔵 [Low][Warning] 第1局 (09:47:57)

- 状态: Phase=action Round=4 Turn=player
- 策略: Greedy
- Seed: 185190508
- 详情:

```
<color=#0099bc><b>DOTWEEN ► </b></color>Target or field is missing/null () ► The object of type 'Material' has been destroyed but you are still trying to access it.
Your script should either check if it is null or you should not destroy the object.

  at (wrapper managed-to-native) UnityEngine.Material.SetFloatImpl(UnityEngine.Material,int,single)
  at UnityEngine.Material.SetFloat (System.String name, System.Single value) [0x00008] in <8cdc7e7e8a17404287a8b220eb9eec72>:0 
  at DG.Tweening.ShortcutExtensions+<>c__DisplayClass22_0.<DOFloat>b__1 (System.Single x) [0x00000] in D:\DG\_Develop\__UNITY_ASSETS\_Demigiant\__DOTween\_DOTween.Assembly\DOTween\ShortcutExtensions.cs:337 
  at DG.Tweening.Plugins.FloatPlugin.EvaluateAndApply (DG.Tweening.Plugins.Options.FloatOptions options, DG.Tweening.Tween t, System.Boolean isRelative, DG.Tweening.Core.DOGetter`1[T] getter, DG.Tweening.Core.DOSetter`1[T] setter, System.Single elapsed, System.Single startValue, System.Single changeValue, System.Single duration, System.Boolean usingInversePosition, System.Int32 newCompletedSteps, DG.Tweening.Core.Enums.UpdateNotice updateNotice) [0x00084] in D:\DG\_Develop\__UNITY_ASSETS\_Demigiant\__DOTween\_DOTween.Assembly\DOTween\Plugins\FloatPlugin.cs:73 
  at DG.Tweening.Core.TweenerCore`3[T1,T2,TPlugOptions].ApplyTween (System.Single prevPosition, System.Int32 prevCompletedLoops, System.Int32 newCompletedSteps, System.Boolean useInversePosition, DG.Tweening.Core.Enums.UpdateMode updateMode, DG.Tweening.Core.Enums.UpdateNotice updateNotice) [0x00030] in D:\DG\_Develop\__UNITY_ASSETS\_Demigiant\__DOTween\_DOTween.Assembly\DOTween\Core\TweenerCore.cs:261 


UnityEngine.Debug:LogWarning (object)
DG.Tweening.Core.Debugger:LogSafeModeCapturedError (object,DG.Tweening.Tween) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Core/Debugger.cs:61)
DG.Tweening.Core.TweenerCore`3<single, single, DG.Tweening.Plugins.Options.FloatOptions>:ApplyTween (single,int,int,bool,DG.Tweening.Core.Enums.UpdateMode,DG.Tweening.Core.Enums.UpdateNotice) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Core/TweenerCore.cs:265)
DG.Tweening.Tween:DoGoto (DG.Tweening.Tween,single,int,DG.Tweening.Core.Enums.UpdateMode) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Tween.cs:266)
DG.Tweening.Core.TweenManager:Goto (DG.Tweening.Tween,single,bool,DG.Tweening.Core.Enums.UpdateMode) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Core/TweenManager.cs:803)
DG.Tweening.Sequence:ApplyInternalCycle (DG.Tweening.Sequence,single,single,DG.Tweening.Core.Enums.UpdateMode,bool,bool,bool) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Sequence.cs:369)
DG.Tweening.Sequence:DoApplyTween (DG.Tweening.Sequence,single,int,int,bool,DG.Tweening.Core.Enums.UpdateMode) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Sequence.cs:275)
DG.Tweening.Sequence:ApplyTween (single,int,int,bool,DG.Tweening.Core.Enums.UpdateMode,DG.Tweening.Core.Enums.UpdateNotice) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Sequence.cs:166)
DG.Tweening.Tween:DoGoto (DG.Tweening.Tween,single,int,DG.Tweening.Core.Enums.UpdateMode) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Tween.cs:266)
DG.Tweening.Core.TweenManager:Update (DG.Tweening.Tween,single,single,bool) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Core/TweenManager.cs:596)
DG.Tweening.Core.TweenManager:Update (DG.Tweening.UpdateType,single,single) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Core/TweenManager.cs:444)
DG.Tweening.Core.DOTweenComponent:Update () (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Core/DOTweenComponent.cs:75)

```

## 🔵 [Low][Warning] 第1局 (09:48:02)

- 状态: Phase=action Round=6 Turn=player
- 策略: Greedy
- Seed: 185190508
- 详情:

```
<color=#0099bc><b>DOTWEEN ► </b></color>Target or field is missing/null () ► The object of type 'Material' has been destroyed but you are still trying to access it.
Your script should either check if it is null or you should not destroy the object.

  at (wrapper managed-to-native) UnityEngine.Material.SetFloatImpl(UnityEngine.Material,int,single)
  at UnityEngine.Material.SetFloat (System.String name, System.Single value) [0x00008] in <8cdc7e7e8a17404287a8b220eb9eec72>:0 
  at DG.Tweening.ShortcutExtensions+<>c__DisplayClass22_0.<DOFloat>b__1 (System.Single x) [0x00000] in D:\DG\_Develop\__UNITY_ASSETS\_Demigiant\__DOTween\_DOTween.Assembly\DOTween\ShortcutExtensions.cs:337 
  at DG.Tweening.Plugins.FloatPlugin.EvaluateAndApply (DG.Tweening.Plugins.Options.FloatOptions options, DG.Tweening.Tween t, System.Boolean isRelative, DG.Tweening.Core.DOGetter`1[T] getter, DG.Tweening.Core.DOSetter`1[T] setter, System.Single elapsed, System.Single startValue, System.Single changeValue, System.Single duration, System.Boolean usingInversePosition, System.Int32 newCompletedSteps, DG.Tweening.Core.Enums.UpdateNotice updateNotice) [0x00084] in D:\DG\_Develop\__UNITY_ASSETS\_Demigiant\__DOTween\_DOTween.Assembly\DOTween\Plugins\FloatPlugin.cs:73 
  at DG.Tweening.Core.TweenerCore`3[T1,T2,TPlugOptions].ApplyTween (System.Single prevPosition, System.Int32 prevCompletedLoops, System.Int32 newCompletedSteps, System.Boolean useInversePosition, DG.Tweening.Core.Enums.UpdateMode updateMode, DG.Tweening.Core.Enums.UpdateNotice updateNotice) [0x00030] in D:\DG\_Develop\__UNITY_ASSETS\_Demigiant\__DOTween\_DOTween.Assembly\DOTween\Core\TweenerCore.cs:261 

 (后续同类消息已静默)
UnityEngine.Debug:LogWarning (object)
DG.Tweening.Core.Debugger:LogSafeModeCapturedError (object,DG.Tweening.Tween) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Core/Debugger.cs:61)
DG.Tweening.Core.TweenerCore`3<single, single, DG.Tweening.Plugins.Options.FloatOptions>:ApplyTween (single,int,int,bool,DG.Tweening.Core.Enums.UpdateMode,DG.Tweening.Core.Enums.UpdateNotice) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Core/TweenerCore.cs:265)
DG.Tweening.Tween:DoGoto (DG.Tweening.Tween,single,int,DG.Tweening.Core.Enums.UpdateMode) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Tween.cs:266)
DG.Tweening.Core.TweenManager:Goto (DG.Tweening.Tween,single,bool,DG.Tweening.Core.Enums.UpdateMode) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Core/TweenManager.cs:803)
DG.Tweening.Sequence:ApplyInternalCycle (DG.Tweening.Sequence,single,single,DG.Tweening.Core.Enums.UpdateMode,bool,bool,bool) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Sequence.cs:369)
DG.Tweening.Sequence:DoApplyTween (DG.Tweening.Sequence,single,int,int,bool,DG.Tweening.Core.Enums.UpdateMode) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Sequence.cs:275)
DG.Tweening.Sequence:ApplyTween (single,int,int,bool,DG.Tweening.Core.Enums.UpdateMode,DG.Tweening.Core.Enums.UpdateNotice) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Sequence.cs:166)
DG.Tweening.Tween:DoGoto (DG.Tweening.Tween,single,int,DG.Tweening.Core.Enums.UpdateMode) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Tween.cs:266)
DG.Tweening.Core.TweenManager:Update (DG.Tweening.Tween,single,single,bool) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Core/TweenManager.cs:596)
DG.Tweening.Core.TweenManager:Update (DG.Tweening.UpdateType,single,single) (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Core/TweenManager.cs:444)
DG.Tweening.Core.DOTweenComponent:Update () (at D:/DG/_Develop/__UNITY_ASSETS/_Demigiant/__DOTween/_DOTween.Assembly/DOTween/Core/DOTweenComponent.cs:75)

```

## 🟡 [Medium][GC泄漏] 第3局 (09:48:39)

- 状态: Phase=start Round=5 Turn=enemy
- 策略: Random
- Seed: 185190508
- 详情:

```
一局 GC 净增 28.8MB（起始 502MB → 结束 531MB），阈值 20MB
```

## 🟡 [Medium][Tween激增] 第12局 (09:51:21)

- 状态: Phase=end Round=6 Turn=enemy
- 策略: Strategic
- Seed: 185190508
- 详情:

```
活跃 tween 数 305（可能有 target destroyed 泄漏）
```

## 🟡 [Medium][Tween激增] 第18局 (09:53:13)

- 状态: Phase=awaken Round=1 Turn=enemy
- 策略: Strategic
- Seed: 185190508
- 详情:

```
活跃 tween 数 316（可能有 target destroyed 泄漏）
```

## 🟡 [Medium][Tween激增] 第18局 (09:53:25)

- 状态: Phase=end Round=7 Turn=enemy
- 策略: Strategic
- Seed: 185190508
- 详情:

```
活跃 tween 数 320（可能有 target destroyed 泄漏）
```

