using System;


namespace EmulatorZ80
{
    /// <summary>
    /// Interface définissant l'accès d'un processeur Z80 émulé
    /// à l'espace mémoire qui lui est attaché.
    /// <br/>
    /// On rappelle que pour ce processeur, outre l'espace-mémoire
    /// proprement dit, il existe un second espace pour les périphériques et
    /// autres entrées / sorties.
    /// </summary>
    public interface IMemorySpace_Z80
    {
        /// <summary>
        /// Lit la valeur d'un octet en mémoire.
        /// </summary>
        /// <param name="address">Adresse-mémoire de l'octet à lire.</param>
        /// <returns>
        /// La valeur lue à l'adresse donnée.
        /// <br/>
        /// Renvoie <code>null</code> si l'adresse en question n'est pas
        /// accessible en lecture.
        /// </returns>
        Byte? ReadMemory(UInt16 address);

        /// <summary>
        /// Écrit la valeur d'un octet en mémoire.
        /// </summary>
        /// <param name="address">Adresse-mémoire de l'octet à écrire.</param>
        /// <param name="value">Valeur de l'octet à écrire.</param>
        /// <returns>
        /// Renvoie <code>true</code> si l'écriture a réussi ;
        /// renvoie <code>false</code> en cas de problème (par exemple :
        /// si l'adresse en question n'est pas accessible en écriture).
        /// </returns>
        Boolean WriteMemory(UInt16 address, Byte value);

        /// <summary>
        /// Lit la valeur d'un octet sur un périphérique (entrée).
        /// </summary>
        /// <param name="address">
        /// Adresse du périphérique sur lequel lire un octet.
        /// </param>
        /// <returns>
        /// L'octet lu sur le périphérique voulu.
        /// <br/>
        /// Renvoie <code>null</code> si le préiphérique en question n'est pas
        /// accessible en lecture.
        /// </returns>
        Byte? Input(Byte address);

        /// <summary>
        /// Écrit un octet sur un périphérique (sortie).
        /// </summary>
        /// <param name="address">
        /// Adresse du périphérique sur lequel écrire un octet.
        /// </param>
        /// <param name="value">Valeur de l'octet à écrire.</param>
        /// <returns>
        /// Renvoie<code>true</code> si l'écriture a réussi ;
        /// renvoie <code>false</code> en cas de problème (par exemple :
        /// si le périphérique en question n'est pas accessible en écriture).
        /// /// </returns>
        Boolean Output(Byte address, Byte value);
    }
}

