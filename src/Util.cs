using System.Text;

static class Util
{
    public static IEnumerable<string> ParseCommaSeparatedList(string text)
    {
        var item = new StringBuilder();

        foreach (var c in text)
        {
            if (c == ',')
            {
                yield return item.ToString();
                item.Clear();
            }
            else
            {
                item.Append(c);
            }
        }

        yield return item.ToString();
    }
}