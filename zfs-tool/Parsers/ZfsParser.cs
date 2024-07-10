using System.Globalization;
using System.Text.RegularExpressions;
using NeoSmart.PrettySize;
using zfs_tool.Models;

namespace zfs_tool.Parsers;

public class ZfsParser
{
    public IEnumerable<ZfsSnapshot> ParseList(string output)
    {
        var reader = new StringReader(output);
        var lineNum = 0;
        string? line;
        var columnWidths = new List<int>();
        while ((line = reader.ReadLine()) != null)
        {
            lineNum++;
            if (lineNum == 1)
            {
                columnWidths = CalculateColumnWidths(line).ToList();
                continue;
            }

            var cols = new List<string>();
            foreach (var colWidth in columnWidths)
            {
                cols.Add(line[..colWidth]);
                line = line[colWidth..];
            }
            
            if (cols.Count != 3)
            {
                continue;
            }
            
            if (!TryParseDate(cols[0].Trim(), out var creation))
            {
                continue;
            }

            var name = cols[1].Trim();
            
            if (!TryParseSize(cols[2].Trim().ToLowerInvariant(), out var writtenInBytes))
            {
                continue;
            }

            yield return new ZfsSnapshot()
            {
                Name = name,
                Creation = creation,
                WrittenBytes = writtenInBytes
            };
        }
    }

    private IEnumerable<int> CalculateColumnWidths(string line)
    {
        var currentWidth = 0;
        var lastChar = ' ';
        foreach (var chr in line)
        {
            if (chr != ' ' && lastChar == ' ' && currentWidth > 0)
            {
                yield return currentWidth;
                currentWidth = 0;
            }
            currentWidth++;
            lastChar = chr;
        }

        if (currentWidth > 0)
        {
            yield return currentWidth;
        }
    }

    private bool TryParseSize(string sizeAsString, out long o)
    {
        var lastChar = sizeAsString[^1];
        var factor = lastChar switch
        {
            'k' => PrettySize.KILOBYTE,
            'm' => PrettySize.MEGABYTE,
            'g' => PrettySize.GIGABYTE,
            't' => PrettySize.TERABYTE,
            'p' => PrettySize.PETABYTE,
            _ => PrettySize.BYTE
        };
        var sizeAsStringTrimmed = factor == PrettySize.BYTE
            ? sizeAsString
            : sizeAsString[..^1];

        if (!double.TryParse(sizeAsStringTrimmed, out var sizeDouble))
        {
            o = long.MinValue;
            return false;
        }
        var sizeInBytesDouble = sizeDouble * factor;
        try
        {
            o = Convert.ToInt64(sizeInBytesDouble);
            return true;
        }
        catch (Exception)
        {
            o = long.MinValue;
        }
        return false;
    }

    private bool TryParseDate(string creationAsString, out DateTime date)
    {
        var trimmedCreationAsString = Regex.Replace(creationAsString[4..], "\\s+", " ");
        
        // Fri Jan 19 15:19 2024
        // --- dd HH:mm:ss yyyy 
        if (DateTime.TryParseExact(trimmedCreationAsString, 
                "MMM d H:mm yyyy", 
                CultureInfo.InvariantCulture, 
                DateTimeStyles.None, 
                out var creation))
        {
            date = creation;
            return true;
        }
        
        date = DateTime.MinValue;
        return false;

    }
}