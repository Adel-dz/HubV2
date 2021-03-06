﻿using System;

namespace easyLib.DB
{
    /*
     * Version: 1
     */ 
    public sealed class CorruptedFileException: CorruptedStreamException
    {

        public CorruptedFileException(string filePath , string message = null , Exception innerException = null) :  //nothrow
            base(message ?? $"Une erreur c’est produite lors d’une opération sur le fichier {filePath}.\n" +
                "Il est probable que le fichier en question soit corrompu." , innerException)
        {
            CorruptedFile = filePath;
        }

        public string CorruptedFile { get; private set; }   //nothrow

    }
}
