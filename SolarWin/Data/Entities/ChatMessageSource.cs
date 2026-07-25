namespace SolarWin.Data.Entities;

/// <summary>How a row entered the local store (design v1.0).</summary>
public enum ChatMessageSource
{
    Api = 0,
    Ws = 1,
    LocalSend = 2,
}
