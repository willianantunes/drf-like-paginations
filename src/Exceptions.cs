using System;

namespace DrfLikePaginations
{
    public class DrfLikePaginationsExceptions : Exception
    {
        public DrfLikePaginationsExceptions(string message) : base(message)
        {
            // Base class of all exceptions 💀
        }
    }

    public class PropertyMustBePresentException : DrfLikePaginationsExceptions
    {
        public PropertyMustBePresentException(string message) : base(message) { }
    }
    
    public class PropertyValueMustBePresentException : DrfLikePaginationsExceptions
    {
        public PropertyValueMustBePresentException(string message) : base(message) { }
    }
}
