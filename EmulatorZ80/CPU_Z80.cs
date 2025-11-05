using System;
using System.IO;


namespace EmulatorZ80
{
    /// <summary>
    /// Classe émulant un processeur Zilog Z80.
    /// </summary>
    public class CPU_Z80
    {
        /* =========================== CONSTANTES =========================== */

        // messages affichés
        private const String ERR_UNREADABLE_ADDRESS =
                "Impossible de lire le contenu de l'adresse ${0:X4} !";
        private const String ERR_UNWRITABLE_ADDRESS =
                "Impossible d'écrire la valeur $1:X2 à l'adresse ${0:X4} !";
        private const String ERR_UNKNOWN_OPCODE =
                "Opcode invalide (${1:X2}) rencontré à l'adresse ${0:X4} !";

        // valeur binaire des "flags" dans le registre CC
        const byte FLAG_C  = 0x01;
        const byte FLAG_N  = 0x02;
        const byte FLAG_PV = 0x04;
        const byte FLAG_H  = 0x10;
        const byte FLAG_Z  = 0x40;
        const byte FLAG_S  = 0x80;

        // adresses particulières
        const ushort RESET_VECTOR = 0x0000;
        const ushort RST0_VECTOR = 0x0000;
        const ushort RST1_VECTOR = 0x0008;
        const ushort RST2_VECTOR = 0x0010;
        const ushort RST3_VECTOR = 0x0018;
        const ushort RST4_VECTOR = 0x0020;
        const ushort RST5_VECTOR = 0x0028;
        const ushort RST6_VECTOR = 0x0030;
        const ushort RST7_VECTOR = 0x0038;
        const ushort IRQ_VECTOR = 0x0038;
        const ushort NMI_VECTOR = 0x0066;

        // masques de sélection de bit
        const byte BYTE_MSB_MASK = 0x80;
        const byte BYTE_ABS_MASK = 0x7f;
        const byte BYTE_BCD_MASK = 0x08;
        const byte BYTE_LSB_MASK = 0x01;
        const ushort WORD_MSB_MASK = 0x8000;


        /* ========================== CHAMPS PRIVÉS ========================= */

        // espace-mémoire attaché au processeur
        // (défini une fois pour toutes à la construction)
        private readonly IMemorySpace_Z80 memSpace;

        /* ~~~~ registres du processeur ~~~~ */

        // registres généraux
        private byte regA;
        private byte regB;
        private byte regC;
        private byte regD;
        private byte regE;
        private byte regH;
        private byte regL;

        // registres de rechange
        private byte regAprime;
        private byte regBprime;
        private byte regCprime;
        private byte regDprime;
        private byte regEprime;
        private byte regHprime;
        private byte regLprime;

        // registres spécialisés
        private byte regI;
        private byte regR;
        private ushort regIX;
        private ushort regIY;
        private ushort regSP;
        private ushort regPC;
        // "flags" composant le "registre F" (état du processeur)
        private bool flagC;
        private bool flagV;
        private bool flagZ;
        private bool flagN;
        private bool flagI;
        private bool flagH;
        private bool flagF;
        private bool flagE;

        // "flip-flop" de masquage des interruptions
        private bool iff1, iff2;
        // mode de réponse à la ligne INT
        private int irqMode;

        // comptage des cycles écoulés
        private ulong cycles;

        // lignes de requêtes d'interruption
        private bool resetLine;
        private bool nmiLine;
        private bool nmiTrig;   // "flag" interne de déclenchement de NMI
        private bool intLine;

        // processeur en attente d'interruption
        private bool halted;

        // politique vis-à-vis des opcodes invalides
        private UnknownOpcodePolicy uoPolicy;


        // objet d'écriture dans le fichier de traçage
        private StreamWriter traceFile;
        // désassembleur pour le traçage
        private Disasm_Z80 traceDisasm;


        /* ========================== CONSTRUCTEUR ========================== */

    }
}

