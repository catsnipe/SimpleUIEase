// Copyright (c) catsnipe
// Released under the MIT license

// Permission is hereby granted, free of charge, to any person obtaining a 
// copy of this software and associated documentation files (the 
// "Software"), to deal in the Software without restriction, including 
// without limitation the rights to use, copy, modify, merge, publish, 
// distribute, sublicense, and/or sell copies of the Software, and to 
// permit persons to whom the Software is furnished to do so, subject to 
// the following conditions:
   
// The above copyright notice and this permission notice shall be 
// included in all copies or substantial portions of the Software.
   
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE 
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION 
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Events;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class SimpleUIEaseEffect
{
    /// <summary>
    /// アニメーションの種類
    /// </summary>
    public SimpleUIEase.eType Type = SimpleUIEase.eType.Fade;
    /// <summary>
    /// 原点となるポジション値。アニメーション終了時はこの値になる
    /// </summary>
    public float              Pos = 0;
    /// <summary>
    /// Fade 以外のタイプで、移動変化量。MoveX であれば -1 が左から、1 が右から
    /// </summary>
    public float              Ratio = -1;
    /// <summary>
    /// イージングの種類
    /// </summary>
    public EaseValue.eEase    Ease = EaseValue.eEase.CubicOut;
    /// <summary>
    /// 識別子タグ
    /// </summary>
    public string             Tag;
    /// <summary>
    /// Custom 選択時、コールされるメソッド
    /// </summary>
    public UnityEvent<float>  OnUpdate;
}

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]

[Serializable]
public class SimpleUIEase : MonoBehaviour
{
    /// <summary>
    /// アニメーションタイプ
    /// </summary>
    public enum eType
    {
        /// <summary>
        /// αフェード
        /// </summary>
        Fade,
        /// <summary>
        /// 横方向移動
        /// </summary>
        MoveX,
        /// <summary>
        /// 縦方向移動
        /// </summary>
        MoveY,
        /// <summary>
        /// 横スケール変更
        /// </summary>
        ScaleX,
        /// <summary>
        /// 縦スケール変更
        /// </summary>
        ScaleY,
        /// <summary>
        /// 回転Z
        /// </summary>
        RotateZ,
        /// <summary>
        /// 回転Y
        /// </summary>
        RotateY,
        /// <summary>
        /// 回転X
        /// </summary>
        RotateX,
        /// <summary>
        /// カスタム
        /// </summary>
        Custom,
    }

    [SerializeField, Range(0.05f, 10f), Tooltip("IN、または OUT するまでの時間を設定します。")]
    public float      TotalTime = 0.3f;

    [SerializeField, Space(10)]
    List<SimpleUIEaseEffect> Effects = new List<SimpleUIEaseEffect>();

    [SerializeField, Space(10), Range(0, 10f), Tooltip("表示アニメーションが始まるまでのディレイタイムを指定します。")]
    public float      DelayTimeBeforeShow = 0;
    [SerializeField, Range(0, 10f), Tooltip("非表示アニメーションが始まるまでのディレイタイムを指定します。")]
    public float      DelayTimeBeforeHide = 0;
    [SerializeField, Space(10), Tooltip("Show / Hide に合わせて自動的に SetActive() を実行します。")]
    public bool       AutoActivate = false;
    [SerializeField, Tooltip("Show / Hide に合わせて自動的に CanvasGroup の入力可否を設定します。")]
    public bool       AutoBlockRaycasts = true;
    [SerializeField, Tooltip("アニメーションをループさせる場合、チェックします。")]
    public bool       Loop = false;
    [SerializeField, Tooltip("Raycast が On になるα値。")]
    public float      RaycastOnValue = 0.5f;

    [SerializeField, Header("Debug"), Range(0, 1), Tooltip("アニメーションの確認を行います。0 が非表示、1 が表示。")]
    float             Value = 1;

    [SerializeField, Header("Event")]
    public UnityEvent OnFadein   = null;
    [SerializeField]
    public UnityEvent OnFadeout  = null;

    public Action<SimpleUIEase, float>
                      OnDebugFading = null;

    Action            OnFadein1  = null;
    Action            OnFadeout1 = null;

    bool              isEasing;

    RectTransform     rectTransform;
    CanvasGroup       canvasGroup;

    CoroutineInfo     co_fadein  = new CoroutineInfo();
    CoroutineInfo     co_fadeout = new CoroutineInfo();

#if UNITY_EDITOR
    List<SimpleUIEaseEffect> compares;
#endif

    /// <summary>
    /// awake
    /// </summary>
    void Awake()
    {
        initCache();

        transitionUpdate(rectTransform, canvasGroup, Value);
    }
    
    /// <summary>
    /// start
    /// </summary>
    void Start()
    {
        if (Value > 0 || co_fadein.CoroutineExists() == true)
        {
            if (AutoActivate == true)
            {
                if (this.gameObject.activeSelf != true)
                {
                    this.SetActive(true);
                }
            }
            if (AutoBlockRaycasts == true)
            {
                canvasGroup.blocksRaycasts = true;
            }
        }
        else
        {
            if (AutoActivate == true)
            {
                if (this.gameObject.activeSelf != false)
                {
                    this.SetActive(false);
                }
            }
            if (AutoBlockRaycasts == true)
            {
                canvasGroup.blocksRaycasts = false;
            }
        }
    }

    void OnEnable()
    {
        this.ResumeSingleCoroutine(co_fadein);
        this.ResumeSingleCoroutine(co_fadeout);
    }
    
#if UNITY_EDITOR
    /// <summary>
    /// on validate
    /// </summary>
    void OnValidate()
    {
        // Warning 回避
        UnityEditor.EditorApplication.delayCall += _OnValidate;
    }
 
    void _OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= _OnValidate;
        if(this == null) return;

        initCache();

        // OnValidate 前と今回の値を比較し、Used の変更やタイプ変更があった場合は rectTransform の値を取り直す
        if (compares == null)
        {
            compares = new List<SimpleUIEaseEffect>();
        }
        for (int i = compares.Count; i < Effects.Count; i++)
        {
            var compare = new SimpleUIEaseEffect();
            compare.Type = Effects[i].Type;
            compares.Add(compare);
        }

        for (int i = 0; i < Effects.Count; i++)
        {
            SimpleUIEaseEffect effect = Effects[i];

            if (effect.Type == compares[i].Type)
            {
                continue;
            }

            compares[i].Type = effect.Type;

            if (effect.Type == eType.MoveX)
            {
                effect.Pos = rectTransform.GetX();
                Debug.Log($"[MoveX] position reset.");
            }
            if (effect.Type == eType.MoveY)
            {
                effect.Pos = rectTransform.GetY();
                Debug.Log($"[MoveY] position reset.");
            }
            if (effect.Type == eType.ScaleX)
            {
                effect.Pos = rectTransform.GetScaleX();
                Debug.Log($"[ScaleX] position reset.");
            }
            if (effect.Type == eType.ScaleY)
            {
                effect.Pos = rectTransform.GetScaleY();
                Debug.Log($"[ScaleY] position reset.");
            }
            if (effect.Type == eType.RotateZ)
            {
                effect.Pos = rectTransform.GetRotateZ();
                Debug.Log($"[RotateZ] position reset.");
            }
            if (effect.Type == eType.RotateY)
            {
                effect.Pos = rectTransform.GetRotateY();
                Debug.Log($"[RotateY] position reset.");
            }
            if (effect.Type == eType.RotateX)
            {
                effect.Pos = rectTransform.GetRotateX();
                Debug.Log($"[RotateX] position reset.");
            }
        }

        transitionUpdate(rectTransform, canvasGroup, Value);
    }
#endif

    /// <summary>
    /// Value を取得
    /// </summary>
    public float GetValue()
    {
        return Value;
    }

    /// <summary>
    /// Value を強制的に変更
    /// </summary>
    /// <param name="value">0:hide～1:show</param>
    public void SetValue(float value)
    {
        //★Value = value の上では？
        stopCoroutine();

        if (value < 0)
        {
            value = 0;
        }
        if (value > 1)
        {
            value = 1;
        }

        if (Value == value)
        {
            return;
        }

        initCache();

        if (value == 1 || co_fadein.CoroutineExists() == true)
        {
            onFadeinInvokeBySetValue();
        }
        else
        if (value == 0 || co_fadeout.CoroutineExists() == true)
        {
            onFadeoutInvokeBySetValue();
        }

        Value = value;
        transitionUpdate(rectTransform, canvasGroup, Value);

        isEasing = false;
    }

    /// <summary>
    /// 最初から表示
    /// </summary>
    public void StartShow(Action fadeinEndFunc = null)
    {
        stopCoroutineFadeout();

        OnFadeout1 = null;
        
        SetValue(0);
        Show(fadeinEndFunc);
    }

    /// <summary>
    /// 表示
    /// </summary>
    public void Show(Action fadeinEndFunc = null)
    {
        stopCoroutineFadeout();

        initCache();

        if (AutoActivate == true)
        {
            if (this.gameObject.activeSelf != true)
            {
                this.SetActive(true);
            }
        }
        if (AutoBlockRaycasts == true)
        {
            // まだ許可は出さない
            if (RaycastOnValue == 0)
            {
                canvasGroup.blocksRaycasts = true;
            }
            else
            {
                canvasGroup.blocksRaycasts = false;
            }
        }

        OnFadein1 = fadeinEndFunc;

        if (Value == 1)
        {
            onFadeinInvokeBySetValue();
            return;
        }

        if (gameObject.activeInHierarchy == false)
        {
            SetValue(1);
            return;
        }

        if (float.IsNaN(Value) == true)
        {
            Value = 0;
        }

        stopCoroutine();
        this.StartSingleCoroutine(ref co_fadein, fadein());
    }

    /// <summary>
    /// 非表示
    /// </summary>
    public void Hide(Action fadeoutEndFunc = null)
    {
        stopCoroutineFadein();

        initCache();

        if (AutoBlockRaycasts == true)
        {
            canvasGroup.blocksRaycasts = false;
        }

        OnFadeout1 = fadeoutEndFunc;

        if (Value == 0)
        {
            onFadeoutInvokeBySetValue();
            return;
        }

        if (gameObject.activeInHierarchy == false)
        {
            SetValue(0);
            return;
        }

        if (float.IsNaN(Value) == true)
        {
            Value = 1;
        }

        stopCoroutine();
        this.StartSingleCoroutine(ref co_fadeout, fadeout());
    }

    /// <summary>
    /// アニメーション停止
    /// </summary>
    public void Stop()
    {
        OnFadein1  = null;
        OnFadeout1 = null;
        stopCoroutine();
    }

    /// <summary>
    /// アニメーション中であれば true
    /// </summary>
    public bool CheckEasing()
    {
        return isEasing;
    }

    /// <summary>
    /// Fadein 中であれば true
    /// </summary>
    public bool CheckFadein()
    {
        return co_fadein.CoroutineExists();
    }

    /// <summary>
    /// Fadeout 中であれば true
    /// </summary>
    public bool CheckFadeout()
    {
        return co_fadeout.CoroutineExists();
    }

    /// <summary>
    /// アニメーション終了待ち
    /// </summary>
    public IEnumerator WaitSync()
    {
        while (isEasing == true)
        {
            yield return null;
        }
    }

    /// <summary>
    /// Ease 配下にある GameObject 入力の禁止(false)・許可(true)
    /// </summary>
    public void SetBlockRaycasts(bool blockRaycasts)
    {
        canvasGroup.blocksRaycasts = blockRaycasts;
    }

    /// <summary>
    /// Ease 配下にある GameObject 入力の禁止(false)・許可(true) 状態を取得
    /// </summary>
    public bool GetBlockRaycasts()
    {
        return canvasGroup.blocksRaycasts;
    }

    /// <summary>
    /// 指定された型の Effect 取得
    /// </summary>
    public SimpleUIEaseEffect GetEffect(eType type)
    {
        foreach (var effect in Effects)
        {
            if (effect.Type == type)
            {
                return effect;
            }
        }
        return null;
    }

    /// <summary>
    /// 指定されたタグの Effect 取得
    /// </summary>
    public SimpleUIEaseEffect GetEffectByTag(string tag)
    {
        foreach (var effect in Effects)
        {
            if (effect.Tag == tag)
            {
                return effect;
            }
        }
        return null;
    }

    /// <summary>
    /// Effect 取得
    /// </summary>
    public List<SimpleUIEaseEffect> GetEffects()
    {
        return Effects;
    }

    /// <summary>
    /// キャッシュ登録
    /// </summary>
    void initCache()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
        if (canvasGroup == null)
        {
            canvasGroup   = GetComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// in out 両方のコルーチン停止
    /// </summary>
    void stopCoroutine()
    {
        stopCoroutineFadein();
        stopCoroutineFadeout();
    }

    void stopCoroutineFadein()
    {
        if (co_fadein.CoroutineExists() == true)
        {
            this.StopSingleCoroutine(ref co_fadein);
            isEasing = false;
        }
    }

    void stopCoroutineFadeout()
    {
        if (co_fadeout.CoroutineExists() == true)
        {
            this.StopSingleCoroutine(ref co_fadeout);
            isEasing = false;
        }
    }

    /// <summary>
    /// fadein
    /// </summary>
    IEnumerator fadein()
    {
        isEasing = true;

        yield return new WaitForSeconds(DelayTimeBeforeShow);

        while (true)
        {
            float time     = Time.time;
            float startVal = Value;

            while (true)
            {
                float value = (Time.time - time) / TotalTime;
                if (value < 0)
                {
                    value = 0;
                }
                if (value > 1)
                {
                    value = 1;
                }
                float value2 = startVal + (1 - startVal) * value;
                if (value2 < 0)
                {
                    value2 = 0;
                }
                if (value2 > 1)
                {
                    value2 = 1;
                }

                Value = value2;

                transitionUpdate(rectTransform, canvasGroup, Value);
            
                if (AutoBlockRaycasts == true)
                {
                    // 完全表示より少し前にレイキャストはONにしておく（ユーザビリティを考えて）
                    if (value >= RaycastOnValue)
                    {
                        canvasGroup.blocksRaycasts = true;
                    }
                }

                if (value >= 1)
                {
                    break;
                }
                yield return null;
            }

            if (Loop == true)
            {
                Value = 0;
            }
            else
            {
                break;
            }
        }

        onFadeinInvoke();

        isEasing = false;

        co_fadein.Clear();
    }

    /// <summary>
    /// fadeout
    /// </summary>
    IEnumerator fadeout()
    {
        isEasing = true;

        yield return new WaitForSeconds(DelayTimeBeforeHide);

        while (true)
        {
            float time     = Time.time;
            float startVal = Value;

            while (true)
            {
                float value = (Time.time - time) / TotalTime;
                if (value < 0)
                {
                    value = 0;
                }
                if (value > 1)
                {
                    value = 1;
                }
                float value2 = startVal + (0 - startVal) * value;
                if (value2 < 0)
                {
                    value2 = 0;
                }
                if (value2 > 1)
                {
                    value2 = 1;
                }

                Value = value2;

                transitionUpdate(rectTransform, canvasGroup, Value);
            
                if (value >= 1)
                {
                    break;
                }
                yield return null;
            }

            if (Loop == true)
            {
                Value = 1;
            }
            else
            {
                break;
            }
        }

        if (AutoActivate == true)
        {
            this.SetActive(false);
        }

        onFadeoutInvoke();

        isEasing = false;

        co_fadeout.Clear();
    }

    void onFadeinInvoke()
    {
        OnFadein?.Invoke();

        OnFadein1?.Invoke();
        OnFadein1   = null;
    }

    void onFadeoutInvoke()
    {
        OnFadeout?.Invoke();

        OnFadeout1?.Invoke();
        OnFadeout1  = null;
    }

    void onFadeinInvokeBySetValue()
    {
        onFadeinInvoke();

        if (AutoActivate == true)
        {
            this.SetActive(true);
        }
        if (AutoBlockRaycasts == true)
        {
            canvasGroup.blocksRaycasts = true;
        }
    }

    void onFadeoutInvokeBySetValue()
    {
        onFadeoutInvoke();

        if (AutoActivate == true)
        {
            this.SetActive(false);
        }
        if (AutoBlockRaycasts == true)
        {
            canvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// 状態更新
    /// </summary>
    void transitionUpdate(RectTransform rectTrans, CanvasGroup group, float value)
    {
        OnDebugFading?.Invoke(this, value);

        foreach (SimpleUIEaseEffect effect in Effects)
        {
            if (effect.Type == eType.Fade)
            {
                if (effect.Ratio == 0)
                {
                    group.alpha = EaseValue.Get(value, 1);
                }
                else
                {
                    group.alpha = EaseValue.Get(value, 1, effect.Pos + effect.Ratio, effect.Pos, effect.Ease);
                }
            }
            else
            if (effect.Type == eType.Custom)
            {
                effect.OnUpdate?.Invoke(value);
            }
            else
            if (effect.Ease != EaseValue.eEase.None)
            {
                if (effect.Type == eType.MoveX)
                {
                    rectSetX(rectTrans, EaseValue.Get(value, 1, effect.Pos + rectGetWidth(rectTrans) * effect.Ratio, effect.Pos, effect.Ease));
                }
                if (effect.Type == eType.MoveY)
                {
                    rectSetY(rectTrans, EaseValue.Get(value, 1, effect.Pos + rectGetHeight(rectTrans) * effect.Ratio, effect.Pos, effect.Ease));
                }
                if (effect.Type == eType.ScaleX)
                {
                    rectSetScaleX(rectTrans, EaseValue.Get(value, 1, effect.Pos + effect.Ratio, effect.Pos, effect.Ease));
                }
                if (effect.Type == eType.ScaleY)
                {
                    rectSetScaleY(rectTrans, EaseValue.Get(value, 1, effect.Pos + effect.Ratio, effect.Pos, effect.Ease));
                }
                if (effect.Type == eType.RotateX)
                {
                    rectSetRotateX(rectTrans, EaseValue.Get(value, 1, effect.Pos + effect.Ratio, effect.Pos, effect.Ease));
                }
                if (effect.Type == eType.RotateY)
                {
                    rectSetRotateY(rectTrans, EaseValue.Get(value, 1, effect.Pos + effect.Ratio, effect.Pos, effect.Ease));
                }
                if (effect.Type == eType.RotateZ)
                {
                    rectSetRotateZ(rectTrans, EaseValue.Get(value, 1, effect.Pos + effect.Ratio, effect.Pos, effect.Ease));
                }
            }
        }
    }

    /// <summary>
    /// 幅を返します
    /// </summary>
    public static float rectGetWidth(RectTransform self)
    {
        return self.rect.size.x;
    }
    
    /// <summary>
    /// 高さを返します
    /// </summary>
    public static float rectGetHeight(RectTransform self)
    {
        return self.rect.size.y;
    }

    /// <summary>
    /// 座標を設定します
    /// </summary>
    static void rectSetX(RectTransform self, float x)
    {
        Vector3 trans = self.gameObject.transform.localPosition;
        trans.x = x;
        self.gameObject.transform.localPosition = trans;
    }

    /// <summary>
    /// 座標を設定します
    /// </summary>
    static void rectSetY(RectTransform self, float y)
    {
        Vector3 trans = self.gameObject.transform.localPosition;
        trans.y = y;
        self.gameObject.transform.localPosition = trans;
    }

    /// <summary>
    /// スケールを設定します
    /// </summary>
    static void rectSetScaleX(RectTransform self, float x)
    {
        Vector3 trans = self.gameObject.transform.localScale;
        trans.x = x;
        self.gameObject.transform.localScale = trans;
    }

    /// <summary>
    /// スケールを設定します
    /// </summary>
    static void rectSetScaleY(RectTransform self, float y)
    {
        Vector3 trans = self.gameObject.transform.localScale;
        trans.y = y;
        self.gameObject.transform.localScale = trans;
    }

    /// <summary>
    /// X回転を設定します
    /// </summary>
    static void rectSetRotateX(RectTransform self, float x)
    {
        Vector3 trans = self.gameObject.transform.localEulerAngles;
        trans.x = x;
        self.gameObject.transform.localEulerAngles = trans;
    }

    /// <summary>
    /// Y回転を設定します
    /// </summary>
    static void rectSetRotateY(RectTransform self, float y)
    {
        Vector3 trans = self.gameObject.transform.localEulerAngles;
        trans.y = y;
        self.gameObject.transform.localEulerAngles = trans;
    }

    /// <summary>
    /// Z回転を設定します
    /// </summary>
    static void rectSetRotateZ(RectTransform self, float z)
    {
        Vector3 trans = self.gameObject.transform.localEulerAngles;
        trans.z = z;
        self.gameObject.transform.localEulerAngles = trans;
    }
}
