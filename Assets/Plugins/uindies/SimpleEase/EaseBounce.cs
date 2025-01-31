using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using static EaseValue;

public class EaseBounce : MonoBehaviour
{
    /// <summary>
    /// 反復横跳びするようなループ値を取得する
    /// </summary>
    /// <param name="time">進行タイム</param>
    /// <param name="endtime">終了タイム. 基本はこれを 1 にして、time を時間経過により 0～1 で与える</param>
    /// <param name="start">開始値</param>
    /// <param name="end">終了値. 開始値～終了値の間をループする</param>
    /// <param name="type">InOut タイプの選択を推奨</param>
    /// <returns>ループ値</returns>
    public static float GetLoop(float time, float endtime, float start, float end, eEase type = eEase.CubicInout)
    {
        float halftime = endtime / 2;

        float v;

        if (time < halftime)
        {
            v = EaseValue.Get(time, halftime, start, end, type);
        }
        else
        {
            v = EaseValue.Get(time - halftime, halftime, end, start, type);
        }

        return v;
    }

    /// <summary>
    /// 反復横跳びするようなループ値を取得する
    /// </summary>
    /// <param name="time01">進行タイム 0～1</param>
    /// <param name="start">開始値</param>
    /// <param name="end">終了値. 開始値～終了値の間をバウンドする</param>
    /// <returns></returns>
    public static float GetLoop(float time01, float start, float end)
    {
        return GetLoop(time01, 1, start, end, eEase.CubicInout);
    }

    /// <summary>
    /// バウンドするようなループ値を取得する
    /// </summary>
    /// <param name="time">進行タイム</param>
    /// <param name="endtime">終了タイム. 基本はこれを 1 にして、time を時間経過により 0～1 で与える</param>
    /// <param name="start">開始値</param>
    /// <param name="end">終了値. 開始値～終了値の間をループする</param>
    /// <param name="intype">In タイプの選択を推奨</param>
    /// <param name="outtype">Out タイプの選択を推奨</param>
    /// <returns></returns>
    public static float GetBound(float time, float endtime, float start, float end, eEase intype = eEase.CubicIn, eEase outtype = eEase.CubicOut)
    {
        float halftime = endtime / 2;

        float v;

        if (time < halftime)
        {
            v = EaseValue.Get(time, halftime, start, end, intype);
        }
        else
        {
            v = EaseValue.Get(time - halftime, halftime, end, start, outtype);
        }

        return v;
    }

    /// <summary>
    /// バウンドするようなループ値を取得する
    /// </summary>
    /// <param name="time01">進行タイム 0～1</param>
    /// <param name="start">開始値</param>
    /// <param name="end">終了値. 開始値～終了値の間をバウンドする</param>
    /// <returns></returns>
    public static float GetBound(float time01, float start, float end)
    {
        return GetBound(time01, 1, start, end, eEase.CubicIn, eEase.CubicOut);
    }
}
