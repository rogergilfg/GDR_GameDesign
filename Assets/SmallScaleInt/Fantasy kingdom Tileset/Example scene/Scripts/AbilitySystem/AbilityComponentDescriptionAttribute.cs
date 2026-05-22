using System;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Decorates ability requirements and steps with a user-facing summary that is displayed in custom editors.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class AbilityComponentDescriptionAttribute : Attribute
    {
        public string Summary { get; }

        public AbilityComponentDescriptionAttribute(string summary)
        {
            Summary = summary ?? string.Empty;
        }
    }
}




