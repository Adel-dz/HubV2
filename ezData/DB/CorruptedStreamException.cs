using System;

namespace easyLib.DB
{
    /*
     * Version: 1
     */ 
    public class CorruptedStreamException: Exception
    {
        const string DEFAULT_MSG = "Une erreur c’est produite lors d’une opération d’entrée/sortie.\n" + 
            "Le flux sous-jacent est probablement corrompu.";


        public CorruptedStreamException(string message = DEFAULT_MSG , Exception innerException = null):    //nothrow
            base(message, innerException)
        { }        
    }
}
