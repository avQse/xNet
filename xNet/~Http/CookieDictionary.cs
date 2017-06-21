using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace xNet
{
    public class Cookie : IEquatable<Cookie>
    {
        public Cookie()
        {
        }

        public Cookie(string name, string value, string domain)
        {
            Name = name;
            Value = value;
            Domain = domain;
        }

        public Cookie(string name, string value, string domain, string path) : this(name, value, domain)
        {
            Path = path;
        }

        public string Name { get; set; }
        public string Value { get; set; }
        public string Domain { get; set; }
        public DateTime Expires { get; set; }

        public string Path { get; set; } = "/";

        public bool Expired
        {
            get => Expires != DateTime.MinValue && Expires.ToLocalTime() <= DateTime.Now;
            set
            {
                if (value) Expires = DateTime.Now;
            }
        }

        public bool Equals(Cookie other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return string.Equals(Name, other.Name) &&
                   string.Equals(Domain, other.Domain) &&
                   string.Equals(Path, other.Path);
        }

        public override string ToString()
        {
            return $"{Name}={Value}; Domain={Domain}; Path={Path}";
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Cookie)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Name != null ? Name.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (Domain != null ? Domain.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Path != null ? Path.GetHashCode() : 0);
                return hashCode;
            }
        }
    }

    /// <summary>
    ///     Представляет коллекцию HTTP-куки.
    /// </summary>
    public class CookieCollection : ICollection
    {
        private readonly Hashtable _domainTable = new Hashtable();
        private readonly List<Cookie> _cookies = new List<Cookie>();

        public Cookie this[string name] => _cookies.FirstOrDefault(c => c.Name == name);
        public Cookie this[int index] => _cookies[index];

        public IEnumerator GetEnumerator()
        {
            return new CookieCollectionEnumerator(this);
        }
        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }
        public int Count => _cookies.Count;
        public object SyncRoot => this;
        public bool IsSynchronized => false;

        public void Add(string name, string value, string domain)
        {
            Add(new Cookie(name, value, domain));
        }

        public void Add(string name, string value, string domain, string path)
        {
            Add(new Cookie(name, value, domain, path));
        }

        public void Add(Cookie cookie)
        {
            string domainKey;
            if (cookie.Domain[0] != '.')
                domainKey = ("." + cookie.Domain).ToLowerInvariant();
            else domainKey = cookie.Domain.ToLowerInvariant();

            PathList pathList;
            lock (_domainTable.SyncRoot)
            {
                pathList = (PathList)_domainTable[domainKey];
                if (pathList == null)
                {
                    pathList = new PathList();
                    AddRemoveDomain(domainKey, pathList);
                }
            }

            List<Cookie> list;
            lock (pathList.SyncRoot)
            {
                list = (List<Cookie>)pathList[cookie.Path];

                if (list == null)
                {
                    list = new List<Cookie>();
                    pathList[cookie.Path] = list;
                }
            }

            if (cookie.Expired)
            {
                lock (list)
                {
                    list.Remove(cookie);
                }
            }
            else
            {
                lock (list)
                {
                    var index = list.FindIndex(c => string.Equals(c.Name, cookie.Name) &&
                                                    string.Equals(c.Domain.TrimStart('.'), cookie.Domain.TrimStart('.')) &&
                                                    string.Equals(c.Path, cookie.Path));

                    if (index != -1)
                    {
                        _cookies.Remove(list[index]);
                        list[index] = cookie;
                    }
                    else
                    {
                        list.Add(cookie);
                    }

                    _cookies.Add(cookie);
                }
            }
        }

        public void Remove(Cookie cookie)
        {
            if (_cookies.Remove(cookie))
            {
                string domainKey;
                if (cookie.Domain[0] != '.') domainKey = ("." + cookie.Domain).ToLowerInvariant();
                else domainKey = cookie.Domain.ToLowerInvariant();

                PathList pathList;
                lock (_domainTable.SyncRoot)
                {
                    pathList = (PathList)_domainTable[domainKey];
                }

                List<Cookie> cookies;
                lock (pathList.SyncRoot)
                {
                    cookies = (List<Cookie>)pathList[cookie.Path];
                }

                cookies.Remove(cookie);

                if (pathList.Count == 0)
                    AddRemoveDomain(domainKey, null);
            }
        }

        public void Remove(string name, string domain, string path)
        {
            var cookie = _cookies.FirstOrDefault(c => string.Equals(c.Name, name) && string.Equals(c.Domain, domain) && string.Equals(c.Path, path));
            if (cookie != null) Remove(cookie);
        }

        private void AddRemoveDomain(string key, PathList value)
        {
            lock (_domainTable.SyncRoot)
            {
                if (value == null) _domainTable.Remove(key);
                else _domainTable[key] = value;
            }
        }

        public bool Contains(string name)
        {
            return _cookies.Any(cookie => string.Equals(cookie.Name, name));
        }

        public List<Cookie> Get(Uri uri)
        {
            var cookies = new List<Cookie>();

            var domainAttributeMatchAnyCookieVariant = new List<string>();
            var domainAttributeMatchOnlyCookieVariantPlain = new List<string>();

            var fqdnRemote = uri.Host;

            domainAttributeMatchAnyCookieVariant.Add(fqdnRemote);
            domainAttributeMatchAnyCookieVariant.Add("." + fqdnRemote);

            var dot = fqdnRemote.IndexOf('.');
            if (dot != -1) domainAttributeMatchAnyCookieVariant.Add(fqdnRemote.Substring(dot));

            BuildCookieCollectionFromDomainMatches(uri, cookies, domainAttributeMatchAnyCookieVariant);
            BuildCookieCollectionFromDomainMatches(uri, cookies, domainAttributeMatchOnlyCookieVariantPlain);

            return cookies;
        }

        private void BuildCookieCollectionFromDomainMatches(Uri uri, List<Cookie> cookies, List<string> domainAttribute)
        {
            for (var i = 0; i < domainAttribute.Count; i++)
            {
                var found = false;
                var defaultAdded = false;
                PathList pathList;

                lock (_domainTable.SyncRoot)
                {
                    pathList = (PathList)_domainTable[domainAttribute[i]];
                }

                if (pathList == null)
                    continue;

                lock (pathList.SyncRoot)
                {
                    foreach (DictionaryEntry entry in pathList)
                    {
                        var path = (string)entry.Key;
                        if (uri.AbsolutePath.StartsWith(CookieParser.CheckQuoted(path)))
                        {
                            found = true;

                            var cc = (List<Cookie>)entry.Value;
                            MergeUpdateCollections(cookies, cc);

                            if (path == "/")
                                defaultAdded = true;
                        }
                        else if (found)
                        {
                            break;
                        }
                    }
                }

                if (!defaultAdded)
                {
                    var cc = (List<Cookie>)pathList["/"];

                    if (cc != null)
                        MergeUpdateCollections(cookies, cc);
                }

                if (pathList.Count == 0)
                    AddRemoveDomain(domainAttribute[i], null);
            }
        }

        private static void MergeUpdateCollections(List<Cookie> destination, List<Cookie> source)
        {
            lock (source)
            {
                for (var idx = 0; idx < source.Count; ++idx)
                {
                    var cookie = source.ElementAt(idx);

                    if (cookie.Expired)
                    {
                        source.Remove(cookie);
                        --idx;
                    }
                    else
                    {
                        destination.Add(cookie);
                    }
                }
            }
        }

        private class CookieCollectionEnumerator : IEnumerator
        {
            private readonly int _count;
            private readonly CookieCollection _сookies;

            private int _index = -1;

            internal CookieCollectionEnumerator(CookieCollection cookies)
            {
                _сookies = cookies;
                _count = cookies.Count;
            }

            // IEnumerator interface

            object IEnumerator.Current
            {
                get
                {
                    if (_index < 0 || _index >= _count)
                        throw new InvalidOperationException();
                    return _сookies[_index];
                }
            }

            bool IEnumerator.MoveNext()
            {
                if (++_index < _count)
                    return true;
                _index = _count;
                return false;
            }

            void IEnumerator.Reset()
            {
                _index = -1;
            }
        }
    }

    internal static class CookieParser
    {
        public static string CheckQuoted(string value)
        {
            if (value.Length < 2 || value[0] != '\"' || value[value.Length - 1] != '\"')
                return value;

            return value.Length == 2 ? string.Empty : value.Substring(1, value.Length - 2);
        }
    }

    internal class PathList
    {
        private readonly SortedList _list = SortedList.Synchronized(new SortedList(PathListComparer.StaticInstance));

        public int Count => _list.Count;

        public ICollection Values => _list.Values;

        public object this[string s]
        {
            get => _list[s];
            set
            {
                lock (SyncRoot)
                {
                    _list[s] = value;
                }
            }
        }

        public object SyncRoot => _list.SyncRoot;

        public int GetCookiesCount()
        {
            var count = 0;
            lock (SyncRoot)
            {
                foreach (List<Cookie> cc in _list.Values)
                    count += cc.Count;
            }
            return count;
        }

        public IEnumerator GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        private class PathListComparer : IComparer
        {
            internal static readonly PathListComparer StaticInstance = new PathListComparer();

            int IComparer.Compare(object ol, object or)
            {
                var pathLeft = CookieParser.CheckQuoted((string)ol);
                var pathRight = CookieParser.CheckQuoted((string)or);
                var ll = pathLeft.Length;
                var lr = pathRight.Length;
                var length = Math.Min(ll, lr);

                for (var i = 0; i < length; ++i)
                    if (pathLeft[i] != pathRight[i])
                        return pathLeft[i] - pathRight[i];
                return lr - ll;
            }
        }
    }
}