using System;
using UnityEngine;

namespace Machamy.GameplayTags.Runtime
{
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class GameplayTagParentAttribute : PropertyAttribute
    {
        private string[] _parentTagNames;
        
        public string[] ParentTagNames => _parentTagNames;
        
        public GameplayTagParentAttribute(string parentTagName, params string[] parentTagNames)
        {
            _parentTagNames = new string[parentTagNames.Length + 1];
            _parentTagNames[0] = parentTagName;
            for (int i = 0; i < parentTagNames.Length; i++)
            {
                _parentTagNames[i + 1] = parentTagNames[i];
            }
        }
    }
}