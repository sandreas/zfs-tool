using System.Globalization;
using System.Text.RegularExpressions;
using NeoSmart.PrettySize;
using zfs_tool.Models;

namespace zfs_tool.Services;

public class ZfsParser
{
    public List<string> Errors { get; set; } = [];
    public long ParseReclaimBytes(string reclaimOutput)
    {
        var reader = new StringReader(reclaimOutput);
        string? line;
        long bytesReclaimed = -1;
        while ((line = reader.ReadLine()) != null)
        {
            var lowerLine = line.ToLowerInvariant();
            if (!lowerLine.StartsWith("would reclaim "))
            {
                continue;
            }
            var words = lowerLine.Split(" ");
            if (!TryParseSize(words[2].Trim().ToLowerInvariant(), out var writtenInBytes))
            {
                continue;
            }
            bytesReclaimed = writtenInBytes;
            break;
        }

        return bytesReclaimed;
    }
    
    public IEnumerable<ZfsSnapshot> ParseList(string output)
    {
        var reader = new StringReader(output);
        var lineNum = 0;
        string? line;
        var columnWidths = new List<int>();
        while ((line = reader.ReadLine()) != null)
        {
            var originalLine = line;
            lineNum++;
            if (lineNum == 1)
            {
                columnWidths = CalculateColumnWidths(line).ToList();
                continue;
            }

            var cols = new List<string>();
            var isZfsOutputBug = false;
            try
            {
                foreach (var colWidth in columnWidths)
                {
                    // bug in zfs - output is shifted / shortened 1 char to the left
                    if (line.Length < colWidth)
                    {
                        isZfsOutputBug = true;
                        line = originalLine;
                        break;
                    }
                    
                    cols.Add(line[..colWidth]);
                    line = line[colWidth..];
                }
                
                
                if (isZfsOutputBug)
                {
                    cols.Clear();
                    foreach (var bogusColWidth in columnWidths)
                    {
                        var shiftedColWidth = Math.Max(0, bogusColWidth - 1);
                        cols.Add(line[..shiftedColWidth]);
                        line = line[shiftedColWidth..];
                    }
                }
            }
            catch (Exception e)
            {
                Errors.Add($"Error in column parsing: {e.Message}");
            }

            
            if (cols.Count != 3)
            {
                continue;
            }
            
            if (!TryParseDate(cols[0].Trim(), out var creation))
            {
                continue;
            }

            var fullName = cols[1].Trim();
            var path = fullName;
            var name = "";
            if (fullName.Contains('@'))
            {
                var parts = fullName.Split('@');
                path = parts[0];
                name = parts[1];
            }
            if (!TryParseSize(cols[2].Trim().ToLowerInvariant(), out var writtenInBytes))
            {
                continue;
            }
            
            yield return new ZfsSnapshot()
            {
                Path = path,
                Name = name,
                FullName = fullName,
                Creation = creation,
                WrittenBytes = writtenInBytes
            };
        }
    }

    private static IEnumerable<int> CalculateColumnWidths(string line)
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

    public static bool TryParseSize(string sizeAsString, out long o)
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

    public static bool TryParseDate(string creationAsString, out DateTime date)
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