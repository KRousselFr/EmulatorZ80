namespace EmulatorZ80
{
    /// <summary>
    /// Énumère les différents mode de réponse aux interruptions
    /// matérielles masquables (ligne INT) du processeur Zilog Z80.
    /// </summary>
    enum Z80_IntrMode
    {

        /// <summary>
        /// Mode d'interruption 0 : compatible avec l'Intel 8080,
        /// le processeur lit un opcode sur le bus de données.
        /// </summary>
        MODE_0 = 0,

        /// <summary>
        /// Mode d'interruption 1 : le processeur exécute le sous-programme
        /// commmençant à l'adresse 0038h.
        /// </summary>
        MODE_1 = 1,

        /// <summary>
        /// Mode d'interruption 2 : le processeur exécute le sous-programme
        /// à l'adresse calculée en prenant le contenu du registre I comme
        /// octet de poids fort et le contenu du bus de données comme octet
        /// de poids faible.
        /// </summary>
        MODE_2 = 2

    }
}
