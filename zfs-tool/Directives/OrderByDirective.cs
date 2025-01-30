namespace zfs_tool.Directives;

using System;
using System.Collections.Generic;
using System.Linq;

public class OrderByDirective<T>
{
    private readonly (Func<T,IComparable> comparator, bool ascending)[] _orderBy;
    private const char FieldSeparator = ',';
    private const char MarkerDesc = '-';
    
    public OrderByDirective(string orderBy, Func<string, Func<T, IComparable>> keySelector)
    {
        _orderBy = orderBy.Split(FieldSeparator).Distinct().Select(s =>
        {
            var trimmed = s.Trim();
            var ascending = !trimmed.StartsWith(MarkerDesc);
            var name = trimmed.TrimStart(MarkerDesc).Trim();
            return (keySelector(name), ascending);
        }).ToArray();
    }
    
    public IEnumerable<T> Apply(IEnumerable<T> subject)
    {
        if (_orderBy.Length == 0)
        {
            return subject;
        }
        var first = _orderBy.First();
        
        var tmp = first.ascending ? subject.OrderBy(first.comparator) : subject.OrderByDescending(first.comparator);
        var rest = _orderBy.Skip(1);
        // var x = DateTime.Now.CompareTo(DateTime.Now);
        foreach(var (cb, asc) in rest)
        {
            tmp = asc ? tmp.ThenBy(cb) : tmp.ThenByDescending(cb);
        }
        return tmp;
    }
    
}
