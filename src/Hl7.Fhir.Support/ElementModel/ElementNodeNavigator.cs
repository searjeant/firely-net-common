using System.Linq;
using System.Collections.Generic;
using System;
using Hl7.Fhir.Utility;

namespace Hl7.Fhir.ElementModel
{
    internal struct ElementNodeNavigator : IElementNavigator, IAnnotated
    {
        private IList<IElementNode> _siblings;
        private int _index;

        public IElementNode Current
        {
            get { return _siblings[_index]; }
        }

        public ElementNodeNavigator(IElementNode wrapped)
        {
            _siblings = new List<IElementNode> { wrapped };
            _index = 0;
        }

        public string Name => Current.Name;

        public string Type => Current.Type;

        public string Location => Current.Location;

        public object Value => Current.Value;

        public IElementNavigator Clone() => new ElementNodeNavigator
        {
            _siblings = this._siblings,
            _index = this._index
        };


        private int nextMatch(IList<IElementNode> nodes, string namefilter=null, int startAfter=-1)
        {
            for(int scan=startAfter+1; scan < nodes.Count; scan++)
            {
                if (namefilter == null || nodes[scan].Name == namefilter)
                    return scan;
            }

            return -1;
        }

        public bool MoveToFirstChild(string nameFilter = null)
        {
            var children = Current.Children().ToList();

            if (!children.Any()) return false;

            var found = nextMatch(children, nameFilter);

            if (found == -1) return false;

            _siblings = children;
            _index = 0;
            return true;
        }

        public bool MoveToNext(string nameFilter = null)
        {
            var found = nextMatch(_siblings, nameFilter, _index);

            if (found == -1) return false;

            _index = found;
            return true;
        }

        public IEnumerable<object> Annotations(Type type)
        {
            if (Current is IAnnotated annotated)
                return annotated.Annotations(type);
            else
                return Enumerable.Empty<object>();
        }
    }


    public static class ElementNodeNavigatorFactory
    {
        [Obsolete("IElementNavigator should be replaced by the IElementNode interface, which is returned by the parsers")]
        public static IElementNavigator ToElementNavigator(this IElementNode node) => new ElementNodeNavigator(node);

    }

}