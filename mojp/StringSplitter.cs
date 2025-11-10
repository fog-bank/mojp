using System;

namespace Mojp;

/// <summary>
/// 文字列分割の遅延処理を行います。
/// </summary>
internal struct StringSplitter
{
    private readonly char sepChar;
    private readonly string sepString;
    private readonly bool sepIsString;
    private int nextStart;

    public StringSplitter(string value, char separator) : this(value)
    {
        sepChar = separator;
    }

    public StringSplitter(string value, string separator) : this(value)
    {
        sepString = separator ?? "\n";
        sepIsString = true;
    }

    private StringSplitter(string value)
    {
        Target = value;

        if (value == null)
            nextStart = -1;
    }

    /// <summary>
    /// 分割対象の文字列を取得します。
    /// </summary>
    public string Target { get; }

    /// <summary>
    /// 現在の分割後の部分文字列を取得します。
    /// </summary>
    /// <remarks><see cref="NeedToSplit"/> が <see langword="true"/> の場合、<see cref="Target"/> そのものが返ります。</remarks>
    public readonly string Current => Target.Substring(CurrentStart, CurrentLength);

    /// <summary>
    /// 現在の部分文字列の開始位置を取得します。
    /// </summary>
    public int CurrentStart { get; private set; }

    /// <summary>
    /// 現在の部分文字列の長さを取得します。
    /// </summary>
    public int CurrentLength { get; private set; }

    /// <summary>
    /// 区切り文字が含まれており、少なくとも 1 回分割したかどうかを示す値を取得します。
    /// </summary>
    public bool NeedToSplit { get; private set; }

    /// <summary>
    /// 分割の事前判定を行います。<see langword="true"/> でないかぎり、各プロパティは動作未定義になります。
    /// </summary>
    public bool TrySplit()
    {
        if (nextStart < 0 || nextStart > Target.Length)
            return false;

        CurrentStart = nextStart;
        int findIndex = sepIsString ?
            Target.IndexOf(sepString, CurrentStart, StringComparison.Ordinal) : Target.IndexOf(sepChar, CurrentStart);

        if (findIndex == -1)
        {
            CurrentLength = Target.Length - CurrentStart;
            nextStart = -1;
            NeedToSplit = CurrentStart != 0;
        }
        else
        {
            CurrentLength = findIndex - CurrentStart;
            nextStart = sepIsString ? findIndex + sepString.Length : findIndex + 1;
            NeedToSplit = true;
        }
        return true;
    }
}
