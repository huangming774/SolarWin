namespace SolarWin.Services;

public interface ITextTokenizer
{
    IEnumerable<string> Tokenize(string text);
}
