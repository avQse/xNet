using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace xNet
{
    public class Cookie : IEquatable<Cookie>
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Domain { get; set; } = "/";
        public string Path { get; set; } = "/";

        public Cookie()
        {
        }

        public Cookie(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public Cookie(string name, string value, string domain) : this(name, value)
        {
            Domain = domain;
        }

        public Cookie(string name, string value, string domain, string path) : this(name, value, domain)
        {
            Path = path;
        }

        public override string ToString()
        {
            return $"{Name}={Value}; Domain={Domain}; Path={Path}";
        }

        public bool Equals(Cookie other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return string.Equals(Name, other.Name) &&
                string.Equals(Domain, other.Domain) &&
                string.Equals(Path, other.Path);
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
                var hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Domain != null ? Domain.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Path != null ? Path.GetHashCode() : 0);
                return hashCode;
            }
        }
    }

    /// <summary>
    /// Представляет коллекцию HTTP-куки.
    /// </summary>
    public class CookieCollection : HashSet<Cookie>
    {
        /// <summary>
        /// Возвращает или задает значение, указывающие, закрыты ли куки для редактирования
        /// </summary>
        /// <value>Значение по умолчанию — <see langword="false"/>.</value>
        public bool IsLocked { get; set; }

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="CookieCollection"/>.
        /// </summary>
        /// <param name="isLocked">Указывает, закрыты ли куки для редактирования.</param>
        public CookieCollection(bool isLocked = false)
        {
            IsLocked = isLocked;
        }

        public void Add(string name, string value)
        {
            Add(new Cookie(name, value));
        }

        public void Add(string name, string value, string domain)
        {
            Add(new Cookie(name, value, domain));
        }

        public void Add(string name, string value, string domain, string path)
        {
            Add(new Cookie(name, value, domain, path));
        }

        public void Remove(string name, string domain, string path)
        {
            RemoveWhere(cookie => string.Equals(cookie.Name, name) &&
                                  string.Equals(cookie.Domain, domain) &&
                                  string.Equals(cookie.Path, path));
        }

        public bool Contains(string name)
        {
            return this.Any(cookie => string.Equals(cookie.Name, name));
        }

    }
}