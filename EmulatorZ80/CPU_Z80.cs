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
        const ushort RESET_PC_ADDRESS = 0x0000;
        const ushort RST0_PC_ADDRESS = 0x0000;
        const ushort RST1_PC_ADDRESS = 0x0008;
        const ushort RST2_PC_ADDRESS = 0x0010;
        const ushort RST3_PC_ADDRESS = 0x0018;
        const ushort RST4_PC_ADDRESS = 0x0020;
        const ushort RST5_PC_ADDRESS = 0x0028;
        const ushort RST6_PC_ADDRESS = 0x0030;
        const ushort RST7_PC_ADDRESS = 0x0038;
        const ushort IRQ_PC_ADDRESS = 0x0038;
        const ushort NMI_PC_ADDRESS = 0x0066;
        static readonly ushort[] RST_PC_ADDRESSES = new ushort[] {
            0x0000, 0x0008, 0x0010, 0x0018, 0x0020, 0x0028, 0x0030, 0x0038
        };

        // masques de sélection de bit
        const byte BYTE_MSB_MASK = 0x80;
        const byte BYTE_ABS_MASK = 0x7f;
        const byte BYTE_BCD_MASK = 0x08;
        const byte BYTE_LSB_MASK = 0x01;
        const ushort WORD_MSB_MASK = 0x8000;
        const ushort WORD_BCD_MASK = 0x0800;


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
        private byte regFprime;
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
        private bool flagN;
        private bool flagPV;
        private bool flagH;
        private bool flagZ;
        private bool flagS;

        // "flip-flop" de masquage des interruptions
        private bool iff1, iff2;
        // mode de réponse à la ligne INT
        private Z80_IntrMode irqMode;

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

        /// <summary>
        /// Contructeur de référence (et unique) de la classe CPU_Z80.
        /// </summary>
        /// <param name="memorySpace">
        /// Espace-mémoire à attacher à ce nouveau processeur.
        /// </param>
        public CPU_Z80(IMemorySpace_Z80 memorySpace)
        {
            this.memSpace = memorySpace;
            this.cycles = 0L;
            this.resetLine = false;
            this.nmiLine = this.nmiTrig = false;
            this.intLine = false;
            this.halted = false;
            this.uoPolicy = UnknownOpcodePolicy.ThrowException;
            this.traceFile = null;
            this.traceDisasm = null;
            Reset();
        }


        /* ======================== MÉTHODES PRIVÉES ======================== */

        /* ~~~~ utilitaires statiques ~~~~ */

        private static byte HiByte(ushort word)
        {
            return (byte)((word >> 8) & 0x00ff);
        }

        private static byte LoByte(ushort word)
        {
            return (byte)(word & 0x00ff);
        }

        private static ushort MakeWord(byte hi, byte lo)
        {
            return (ushort)((hi << 8) | lo);
        }

        /* ~~~~ accès à l'espace mémoire ~~~~ */

        private byte ReadMem(ushort addr)
        {
            byte? memval = this.memSpace.ReadMemory(addr);
            if (!(memval.HasValue)) {
                throw new AddressUnreadableException(
                        addr,
                        String.Format(ERR_UNREADABLE_ADDRESS,
                                      addr));
            }
            this.cycles += 3L;
            return memval.Value;
        }

        private void WriteMem(ushort addr, byte val)
        {
            bool ok = this.memSpace.WriteMemory(addr, val);
            if (!ok) {
                throw new AddressUnwritableException(
                        addr,
                        String.Format(ERR_UNWRITABLE_ADDRESS,
                                      addr, val));
            }
            this.cycles += 3L;
        }

        private byte InputByte(byte addr)
        {
            byte? periphVal = this.memSpace.Input(addr);
            if (!(periphVal.HasValue)) {
                throw new AddressUnreadableException(
                        addr,
                        String.Format(ERR_UNREADABLE_ADDRESS,
                                      addr));
            }
            this.cycles += 4L;
            return periphVal.Value;
        }

        private void OutputByte(byte addr, byte val)
        {
            bool ok = this.memSpace.Output(addr, val);
            if (!ok) {
                throw new AddressUnwritableException(
                        addr,
                        String.Format(ERR_UNWRITABLE_ADDRESS,
                                      addr, val));
            }
            this.cycles += 4L;
        }

        /* ~~~~ implantation des modes d'adressage ~~~~ */

        private byte AddrModeImmediateValue()
        {
            byte val = ReadMem(this.regPC);
            this.regPC++;
            return val;
        }

        private ushort AddrModeImmediateExtendedValue()
        {
            byte lo = ReadMem(this.regPC);
            this.regPC++;
            byte hi = ReadMem(this.regPC);
            this.regPC++;
            return MakeWord(hi, lo);
        }

        private sbyte AddrModeIndexDisplacement()
        {
            byte val = ReadMem(this.regPC);
            this.regPC++;
            return (sbyte)val;
        }

        /* ~~~~ accès à la pile ~~~~ */

        private void PushByte(byte val)
        {
            this.regSP--;
            WriteMem(this.regSP, val);
        }

        private void PushWord(ushort val)
        {
            PushByte(LoByte(val));
            PushByte(HiByte(val));
        }

        private byte PopByte()
        {
            byte val = ReadMem(this.regSP);
            this.regSP++;
            return val;
        }

        private ushort PopWord()
        {
            byte hi = PopByte();
            byte lo = PopByte();
            return MakeWord(hi, lo);
        }

        /* ~~~~ gestion des "flags" ~~~~ */

        private void SetSZ(byte val)
        {
            this.flagZ = (val == 0x00);
            this.flagS = ((val & BYTE_MSB_MASK) != 0);
        }

        private void SetH(byte baseVal, byte diff, byte res)
        {
            this.flagH = ( ((baseVal & BYTE_BCD_MASK) != 0) &&
                           ((diff & BYTE_BCD_MASK) != 0) )
                      || ( ((diff & BYTE_BCD_MASK) != 0) &&
                           ((res & BYTE_BCD_MASK) == 0) )
                      || ( ((res & BYTE_BCD_MASK) == 0) &&
                           ((baseVal & BYTE_BCD_MASK) != 0) );
        }

        private void SetV(byte baseVal, byte op, byte res)
        {
            /*
             * V est activé :
             * - si deux positifs donnent un négatif, ou :
             * - si deux négatifs donnent un positif
             */
            this.flagPV = ( ((baseVal & BYTE_MSB_MASK) != 0) &&
                            ((op & BYTE_MSB_MASK) != 0) &&
                            ((res & BYTE_MSB_MASK) == 0) )
                       || ( ((baseVal & BYTE_MSB_MASK) == 0) &&
                            ((op & BYTE_MSB_MASK) == 0) &&
                            ((res & BYTE_MSB_MASK) != 0) );
            ;
        }

        /* ~~~~ implantation des opérations de l'ALU ~~~~ */

        private byte Do8bitAdd(byte baseVal, byte add, bool useC)
        {
            /* somme proprement dite */
            int sum = baseVal + add;
            if (useC && this.flagC) sum++;
            byte res = (byte)sum;
            /* "flags" */
            SetSZ(res);
            this.flagN = false;
            this.flagC = (sum > 0xff);
            SetH(baseVal, add, res);
            SetV(baseVal, add, res);
            /* retourne le résultat */
            return res;
        }

        private ushort Do16bitAdd(ushort baseVal, ushort add, bool useC)
        {
            /* somme proprement dite */
            int sum = baseVal + add;
            if (useC && this.flagC) sum++;
            ushort res = (ushort)sum;
            /* "flags" */
            this.flagN = false;
            this.flagC = (sum > 0xffff);
            this.flagH = ( ((baseVal & WORD_BCD_MASK) != 0) &&
                           ((add & WORD_BCD_MASK) != 0))
                      || ( ((add & WORD_BCD_MASK) != 0) &&
                           ((res & WORD_BCD_MASK) == 0))
                      || ( ((res & WORD_BCD_MASK) == 0) &&
                           ((baseVal & WORD_BCD_MASK) != 0));
            /* retourne le résultat */
            return res;
        }

        private byte Do8bitSub(byte baseVal, byte diff, bool useC)
        {
            /* soustraction == addition du nombre opposé */
            if (useC) diff++;
            byte add = (byte)(-diff);
            return Do8bitAdd(baseVal, add, false);
        }

        /* ~~~~ implantation des instructions ~~~~ */

        private void InstrANDA(byte val)
        {
            byte res = (byte)(this.regA & val);
            SetSZ(res);
            SetV(this.regA, val, res);
            this.flagH = true;
            this.flagN = false;
            this.flagC = false;
            this.regA = res;
            this.cycles++;
        }

        private void InstrCALL(bool condOK)
        {
            ushort addr = AddrModeImmediateExtendedValue();
            this.cycles++;
            if (condOK) {
                PushWord(this.regPC);
                this.regPC = addr;
                this.cycles++;
            }
        }

        private void InstrCCF()
        {
            this.flagH = this.flagC;
            this.flagC = !(this.flagC);
            this.flagN = false;
            this.cycles++;
        }

        private void InstrCPLA()
        {
            this.regA = (byte)(~this.regA);
            this.flagH = true;
            this.flagN = true;
            this.cycles++;
        }

        private void InstrDAA()
        {
            int hiDigit = (this.regA >> 4) & 0x0f;
            int loDigit = this.regA & 0x0f;
            if (this.flagN) {
                /* soustractions */
                if (this.flagC) {
                    if (this.flagH) {
                        // C && H
                        if ((hiDigit >= 6) && (loDigit >= 6)) {
                            this.regA = Do8bitAdd(this.regA, 0x9a, false);
                            this.flagC = true;
                        }
                    } else {
                        // C && !H
                        if ((hiDigit >= 7) && (loDigit <= 9)) {
                            this.regA = Do8bitAdd(this.regA, 0xa0, false);
                            this.flagC = true;
                        }
                    }
                } else {
                    if (this.flagH) {
                        // !C && H
                        if ((hiDigit <= 8) && (loDigit >= 6)) {
                            this.regA = Do8bitAdd(this.regA, 0xfa, false);
                            this.flagC = false;
                        }
                    } else {
                        // !C && !H
                        if ((hiDigit <= 9) && (loDigit <= 9)) {
                            this.regA = Do8bitAdd(this.regA, 0x00, false);
                            this.flagC = false;
                        }
                    }
                }
            } else {
                /* additions */
                if (this.flagC) {
                    if (this.flagH) {
                        // C && H
                        if ((hiDigit <= 3) && (loDigit <= 3)) {
                            this.regA = Do8bitAdd(this.regA, 0x66, false);
                            this.flagC = true;
                        }
                    } else {
                        // C && !H
                        if (hiDigit <= 2) {
                            if (loDigit <= 9) {
                                this.regA = Do8bitAdd(this.regA, 0x60, false);
                            } else {
                                this.regA = Do8bitAdd(this.regA, 0x66, false);
                            }
                            this.flagC = true;
                        }
                    }
                } else {
                    if (this.flagH) {
                        // !C && H
                        if (hiDigit <= 9) {
                            if (loDigit <= 3) {
                                this.regA = Do8bitAdd(this.regA, 0x06, false);
                                this.flagC = false;
                            }
                        } else {
                            if (loDigit <= 3) {
                                this.regA = Do8bitAdd(this.regA, 0x66, false);
                                this.flagC = true;
                            }
                        }
                    } else {
                        // !C && !H
                        if (loDigit <= 9) {
                            if (hiDigit <= 9) {
                                this.regA = Do8bitAdd(this.regA, 0x00, false);
                                this.flagC = false;
                            } else {
                                this.regA = Do8bitAdd(this.regA, 0x60, false);
                                this.flagC = true;
                            }
                        } else {
                            if (hiDigit <= 8) {
                                this.regA = Do8bitAdd(this.regA, 0x06, false);
                                this.flagC = false;
                            } else {
                                this.regA = Do8bitAdd(this.regA, 0x66, false);
                                this.flagC = true;
                            }
                        }
                    }
                }
            }
            this.cycles++;
        }
        
        // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        private byte InstrDEC(byte val)
        {
            byte oldVal = val;
            val--;
            SetSZ(val);
            this.flagPV = (oldVal == 0x80);
            this.flagN = true;
            SetH(oldVal, 0xff, val);
            this.cycles++;
            return val;
        }

        private ushort InstrDEC16(ushort val)
        {
            val--;
            this.cycles += 3L;
            return val;
        }

        private void InstrDI()
        {
            this.iff1 = this.iff2 = false;
            this.cycles++;
        }

        private void InstrDJNZ()
        {
            sbyte dpl = AddrModeIndexDisplacement();
            this.cycles += 2L;
            this.regB--;
            if (this.regB != 0x00) {
                this.regPC = (ushort)(this.regPC + dpl);
                this.cycles += 5L;
            }
        }

        // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        private void InstrEI()
        {
            this.iff1 = this.iff2 = true;
            this.cycles++;
        }

        private void InstrEXAF()
        {
            byte val8 = this.regA;
            this.regA = this.regAprime;
            this.regAprime = val8;
            val8 = this.RegisterF;
            this.RegisterF = this.regFprime;
            this.regFprime = val8;
            this.cycles++;
        }

        private void InstrEXDEHL()
        {
            byte val8 = this.regH;
            this.regH = this.regD;
            this.regD = val8;
            val8 = this.regL;
            this.regL = this.regE;
            this.regE = val8;
            this.cycles++;
        }

        private ushort InstrEXSP(ushort val16)
        {
            byte lo = ReadMem(this.regSP);
            WriteMem(this.regSP, LoByte(val16));
            this.regSP++;
            byte hi = ReadMem(this.regSP);
            WriteMem(this.regSP, HiByte(val16));
            this.regSP--;
            this.cycles += 4L;
            return MakeWord(hi, lo);
        }

        private void InstrEXX()
        {
            byte val8 = this.regB;
            this.regB = this.regBprime;
            this.regBprime = val8;
            val8 = this.regC;
            this.regC = this.regCprime;
            this.regCprime = val8;
            val8 = this.regD;
            this.regD = this.regDprime;
            this.regDprime = val8;
            val8 = this.regE;
            this.regE = this.regEprime;
            this.regEprime = val8;
            val8 = this.regH;
            this.regH = this.regHprime;
            this.regHprime = val8;
            val8 = this.regL;
            this.regL = this.regLprime;
            this.regLprime = val8;
            this.cycles++;
        }

        // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        private void InstrHALT()
        {
            this.halted = true;
            this.cycles++;
        }

        // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        private void InstrIM(Z80_IntrMode im)
        {
            this.irqMode = im;
            this.cycles += 2L;
        }

        // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        private byte InstrIN(byte addr)
        {
            byte val = InputByte(addr);
            this.cycles++;
            return val;
        }

        // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        private byte InstrINC(byte val)
        {
            byte oldVal = val;
            val++;
            SetSZ(val);
            this.flagPV = (oldVal == 0x7f);
            this.flagN = false;
            SetH(oldVal, 1, val);
            this.cycles++;
            return val;
        }

        private ushort InstrINC16(ushort val)
        {
            val++;
            this.cycles += 3L;
            return val;
        }

        // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        private void InstrJP(bool condOK)
        {
            ushort addr = AddrModeImmediateExtendedValue();
            if (condOK) {
                this.regPC = addr;
            }
            this.cycles++;
        }

        private void InstrJR(bool condOK)
        {
            sbyte dpl = AddrModeIndexDisplacement();
            this.cycles++;
            if (condOK) {
                this.regPC = (ushort)(this.regPC + dpl);
                this.cycles += 5L;
            }
        }

        // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        private void InstrLDAI()
        {
            this.regA = this.regI;
            SetSZ(this.regI);
            this.flagPV = this.iff2;
            this.flagH = false;
            this.flagN = false;
            this.cycles += 3L;
        }

        private void InstrLDAR()
        {
            this.regA = this.regR;
            SetSZ(this.regR);
            this.flagPV = this.iff2;
            this.flagH = false;
            this.flagN = false;
            this.cycles += 3L;
        }

        // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        private void InstrNOP()
        {
            /* ne rien faire, sinon passer des cycles */
            this.cycles += 1L;
        }

        // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        private void InstrORA(byte val)
        {
            byte res = (byte)(this.regA | val);
            SetSZ(res);
            SetV(this.regA, val, res);
            this.flagH = false;
            this.flagN = false;
            this.flagC = false;
            this.regA = res;
            this.cycles++;
        }

        // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        private void InstrOUT(byte addr, byte val)
        {
            OutputByte(addr, val);
            this.cycles++;
        }

        // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        private ushort InstrPOP()
        {
            ushort reg16 = PopWord();
            this.cycles++;
            return reg16;
        }

        private void InstrPUSH(ushort reg16)
        {
            PushWord(reg16);
            this.cycles += 2L;
        }

        // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        private void InstrRET(bool condOK)
        {
            if (condOK) {
                this.regPC = PopWord();
            }
            this.cycles += 2L;
        }

        // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        private void InstrRETN()
        {
            this.regPC = PopWord();
            this.iff1 = this.iff2;
            this.cycles += 2L;
        }

        // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        private byte InstrRL(byte val)
        {
            bool oldC = this.flagC;
            this.flagC = ((val & BYTE_MSB_MASK) != 0);
            this.FlagN = false;
            this.flagH = false;
            val <<= 1;
            val &= 0xfe;
            if (oldC) val |= BYTE_LSB_MASK;
            return val;
        }

        private byte InstrRLC(byte val)
        {
            this.flagC = ((val & BYTE_MSB_MASK) != 0);
            this.FlagN = false;
            this.flagH = false;
            val <<= 1;
            val &= 0xfe;
            if (this.flagC) val |= BYTE_LSB_MASK;
            return val;
        }

        private byte InstrRR(byte val)
        {
            bool oldC = this.flagC;
            this.flagC = ((val & BYTE_LSB_MASK) != 0);
            this.FlagN = false;
            this.flagH = false;
            val >>= 1;
            val &= 0x7f;
            if (oldC) val |= BYTE_MSB_MASK;
            return val;
        }

        private byte InstrRRC(byte val)
        {
            this.flagC = ((val & BYTE_LSB_MASK) != 0);
            this.FlagN = false;
            this.flagH = false;
            val >>= 1;
            val &= 0x7f;
            if (this.flagC) val |= BYTE_MSB_MASK;
            return val;
        }

        private void InstrRST(byte numVector)
        {
            PushWord(this.regPC);
            this.regPC = RST_PC_ADDRESSES[numVector];
            this.cycles += 2;
        }

        private void InstrSCF()
        {
            this.flagC = true;
            this.flagH = false;
            this.flagN = false;
            this.cycles++;
        }

        private void InstrXORA(byte val)
        {
            byte res = (byte)(this.regA ^ val);
            SetSZ(res);
            SetV(this.regA, val, res);
            this.flagH = false;
            this.flagN = false;
            this.flagC = false;
            this.regA = res;
            this.cycles++;
        }

        /* ~~~~ analyse des opcodes ~~~~ */

        private bool ExecCBopcode(byte opcode)
        {
            ;
            // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private bool ExecDDopcode(byte opcode)
        {
            ;
            // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private bool ExecEDopcode(byte opcode)
        {
            ;
            // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private bool ExecFDopcode(byte opcode)
        {
            ;
            // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private bool ExecStandardOpcode(byte opcode)
        {
            byte val8, port;
            ushort addr;

            switch (opcode) {
                case 0x00:
                    // NOP
                    InstrNOP();
                    return true;
                case 0x01:
                    // LD BC, #nnnn
                    this.RegisterBC = AddrModeImmediateExtendedValue();
                    this.cycles++;
                    return true;
                case 0x02:
                    // LD (BC), A
                    addr = this.RegisterBC;
                    WriteMem(addr, this.regA);
                    this.cycles++;
                    return true;
                case 0x03:
                    // INC BC
                    this.RegisterBC = InstrINC16(this.RegisterBC);
                    return true;
                case 0x04:
                    // INC B
                    this.regB = InstrINC(this.regB);
                    return true;
                case 0x05:
                    // DEC B
                    this.regB = InstrDEC(this.regB);
                    return true;
                case 0x06:
                    // LD B, #nn
                    this.regB = AddrModeImmediateValue();
                    this.cycles++;
                    return true;
                case 0x07:
                    // RLCA
                    this.regA = InstrRLC(this.regA);
                    this.cycles++;
                    return true;
                case 0x08:
                    // EX AF, AF'
                    InstrEXAF();
                    return true;
                case 0x09:
                    // ADD HL, BC
                    this.RegisterHL = Do16bitAdd(this.RegisterHL,
                                                 this.RegisterBC,
                                                 false);
                    this.cycles += 8L;
                    return true;
                case 0x0a:
                    // LD A, (BC)
                    addr = this.RegisterBC;
                    this.regA = ReadMem(addr);
                    this.cycles++;
                    return true;
                case 0x0b:
                    // DEC BC
                    this.RegisterBC = InstrDEC16(this.RegisterBC);
                    return true;
                case 0x0c:
                    // INC C
                    this.regC = InstrINC(this.regC);
                    return true;
                case 0x0d:
                    // DEC C
                    this.regC = InstrDEC(this.regC);
                    return true;
                case 0x0e:
                    // LD C, #nn
                    this.regC = AddrModeImmediateValue();
                    this.cycles++;
                    return true;
                case 0x0f:
                    // RRCA
                    this.regA = InstrRRC(this.regA);
                    this.cycles++;
                    return true;

                case 0x10:
                    // DJNZ rel
                    InstrDJNZ();
                    return true;
                case 0x11:
                    // LD DE, #nn
                    this.RegisterDE = AddrModeImmediateExtendedValue();
                    this.cycles++;
                    return true;
                case 0x12:
                    // LD (DE), A
                    addr = this.RegisterDE;
                    WriteMem(addr, this.regA);
                    this.cycles++;
                    return true;
                case 0x13:
                    // INC DE
                    this.RegisterDE = InstrINC16(this.RegisterDE);
                    return true;
                case 0x14:
                    // INC D
                    this.regD = InstrINC(this.regD);
                    return true;
                case 0x15:
                    // DEC D
                    this.regD = InstrDEC(this.regD);
                    return true;
                case 0x16:
                    // LD D, #nn
                    this.regD = AddrModeImmediateValue();
                    this.cycles++;
                    return true;
                case 0x17:
                    // RLA
                    this.regA = InstrRL(this.regA);
                    this.cycles++;
                    return true;
                case 0x18:
                    // JR rel
                    InstrJR(true);
                    return true;
                case 0x19:
                    // ADD HL, DE
                    this.RegisterHL = Do16bitAdd(this.RegisterHL,
                                                 MakeWord(this.regD, this.regE),
                                                 false);
                    this.cycles += 8L;
                    return true;
                case 0x1a:
                    // LD A, (DE)
                    addr = this.RegisterDE;
                    this.regA = ReadMem(addr);
                    this.cycles++;
                    return true;
                case 0x1b:
                    // DEC DE
                    this.RegisterDE = InstrDEC16(this.RegisterDE);
                    return true;
                case 0x1c:
                    // INC E
                    this.regE = InstrINC(this.regE);
                    return true;
                case 0x1d:
                    // DEC E
                    this.regE = InstrDEC(this.regE);
                    return true;
                case 0x1e:
                    // LD E, #nn
                    this.regE = AddrModeImmediateValue();
                    this.cycles++;
                    return true;
                case 0x1f:
                    // RRA
                    this.regA = InstrRR(this.regA);
                    this.cycles++;
                    return true;

                case 0x20:
                    // JR NZ, rel
                    InstrJR(!(this.flagZ));
                    return true;
                case 0x21:
                    // LD HL, #nn
                    this.RegisterHL = AddrModeImmediateExtendedValue();
                    this.cycles++;
                    return true;
                case 0x22:
                    // LD (nnnn), HL
                    addr = AddrModeImmediateExtendedValue();
                    WriteMem(addr, this.regL);
                    addr++;
                    WriteMem(addr, this.regH);
                    this.cycles++;
                    return true;
                case 0x23:
                    // INC HL
                    this.RegisterHL = InstrINC16(this.RegisterHL);
                    return true;
                case 0x24:
                    // INC H
                    this.regH = InstrINC(this.regH);
                    return true;
                case 0x25:
                    // DEC H
                    this.regH = InstrDEC(this.regH);
                    return true;
                case 0x26:
                    // LD H, #nn
                    this.regH = AddrModeImmediateValue();
                    this.cycles++;
                    return true;
                case 0x27:
                    // DAA
                    InstrDAA();
                    return true;
                case 0x28:
                    // JR Z, rel
                    InstrJR(this.flagZ);
                    return true;
                case 0x29:
                    // ADD HL, HL
                    this.RegisterHL = Do16bitAdd(this.RegisterHL,
                                                 this.RegisterHL,
                                                 false);
                    this.cycles += 8L;
                    return true;
                case 0x2a:
                    // LD HL, (nnnn)
                    addr = AddrModeImmediateExtendedValue();
                    this.regL = ReadMem(addr);
                    addr++;
                    this.regH = ReadMem(addr);
                    this.cycles++;
                    return true;
                case 0x2b:
                    // DEC HL
                    this.RegisterHL = InstrDEC16(this.RegisterHL);
                    return true;
                case 0x2c:
                    // INC L
                    this.regL = InstrINC(this.regL);
                    return true;
                case 0x2d:
                    // DEC L
                    this.regL = InstrDEC(this.regL);
                    return true;
                case 0x2e:
                    // LD L, #nn
                    this.regL = AddrModeImmediateValue();
                    this.cycles++;
                    return true;
                case 0x2f:
                    // CPL A
                    InstrCPLA();
                    return true;

                case 0x30:
                    // JR NC, rel
                    InstrJR(!(this.flagC));
                    return true;
                case 0x31:
                    // LD SP, #nn
                    this.regSP = AddrModeImmediateExtendedValue();
                    this.cycles++;
                    return true;
                case 0x32:
                    // LD (nnnn), A
                    addr = AddrModeImmediateExtendedValue();
                    WriteMem(addr, this.regA);
                    this.cycles++;
                    return true;
                case 0x33:
                    // INC SP
                    this.regSP = InstrINC16(this.regSP);
                    return true;
                case 0x34:
                    // INC (HL)
                    addr = this.RegisterHL;
                    val8 = ReadMem(addr);
                    val8 = InstrINC(val8);
                    WriteMem(addr, val8);
                    this.cycles++;
                    return true;
                case 0x35:
                    // DEC (HL)
                    addr = this.RegisterHL;
                    val8 = ReadMem(addr);
                    val8 = InstrDEC(val8);
                    WriteMem(addr, val8);
                    this.cycles++;
                    return true;
                case 0x36:
                    // LD (HL), #nn
                    val8 = AddrModeImmediateValue();
                    addr = this.RegisterHL;
                    WriteMem(addr, val8);
                    this.cycles++;
                    return true;
                case 0x37:
                    // SCF
                    InstrSCF();
                    return true;
                case 0x38:
                    // JR C, rel
                    InstrJR(this.flagC);
                    return true;
                case 0x39:
                    // ADD HL, SP
                    this.RegisterHL = Do16bitAdd(this.RegisterHL,
                                                 this.regSP,
                                                 false);
                    this.cycles += 8L;
                    return true;
                case 0x3a:
                    // LD A, (nnnn)
                    addr = AddrModeImmediateExtendedValue();
                    this.regA = ReadMem(addr);
                    this.cycles++;
                    return true;
                case 0x3b:
                    // DEC SP
                    this.regSP = InstrDEC16(this.regSP);
                    return true;
                case 0x3c:
                    // INC A
                    this.regA = InstrINC(this.regA);
                    return true;
                case 0x3d:
                    // DEC A
                    this.regA = InstrDEC(this.regA);
                    return true;
                case 0x3e:
                    // LD A, #nn
                    this.regA = AddrModeImmediateValue();
                    this.cycles++;
                    return true;
                case 0x3f:
                    // CCF
                    InstrCCF();
                    return true;

                case 0x40:
                    // LD B, B
                    //this.regB = this.regB;
                    this.cycles++;
                    return true;
                case 0x41:
                    // LD B, C
                    this.regB = this.regC;
                    this.cycles++;
                    return true;
                case 0x42:
                    // LD B, D
                    this.regB = this.regD;
                    this.cycles++;
                    return true;
                case 0x43:
                    // LD B, E
                    this.regB = this.regE;
                    this.cycles++;
                    return true;
                case 0x44:
                    // LD B, H
                    this.regB = this.regH;
                    this.cycles++;
                    return true;
                case 0x45:
                    // LD B, L
                    this.regB = this.regL;
                    this.cycles++;
                    return true;
                case 0x46:
                    // LD B, (HL)
                    addr = this.RegisterHL;
                    this.regB = ReadMem(addr);
                    this.cycles++;
                    return true;
                case 0x47:
                    // LD B, A
                    this.regB = this.regA;
                    this.cycles++;
                    return true;
                case 0x48:
                    // LD C, B
                    this.regC = this.regB;
                    this.cycles++;
                    return true;
                case 0x49:
                    // LD C, C
                    // this.regC = this.regC;
                    this.cycles++;
                    return true;
                case 0x4a:
                    // LD C, D
                    this.regC = this.regD;
                    this.cycles++;
                    return true;
                case 0x4b:
                    // LD C, E
                    this.regC = this.regE;
                    this.cycles++;
                    return true;
                case 0x4c:
                    // LD C, H
                    this.regC = this.regH;
                    this.cycles++;
                    return true;
                case 0x4d:
                    // LD C, L
                    this.regC = this.regL;
                    this.cycles++;
                    return true;
                case 0x4e:
                    // LD C, (HL)
                    addr = this.RegisterHL;
                    this.regC = ReadMem(addr);
                    this.cycles++;
                    return true;
                case 0x4f:
                    // LD C, A
                    this.regC = this.regB;
                    this.cycles++;
                    return true;

                case 0x50:
                    // LD D, B
                    this.regD = this.regB;
                    this.cycles++;
                    return true;
                case 0x51:
                    // LD D, C
                    this.regD = this.regC;
                    this.cycles++;
                    return true;
                case 0x52:
                    // LD D, D
                    // this.regD = this.regD;
                    this.cycles++;
                    return true;
                case 0x53:
                    // LD D, E
                    this.regD = this.regE;
                    this.cycles++;
                    return true;
                case 0x54:
                    // LD D, H
                    this.regD = this.regH;
                    this.cycles++;
                    return true;
                case 0x55:
                    // LD D, L
                    this.regD = this.regL;
                    this.cycles++;
                    return true;
                case 0x56:
                    // LD D, (HL)
                    addr = this.RegisterHL;
                    this.regC = ReadMem(addr);
                    this.cycles++;
                    return true;
                case 0x57:
                    // LD D, A
                    this.regD = this.regA;
                    this.cycles++;
                    return true;
                case 0x58:
                    // LD E, B
                    this.regE = this.regB;
                    this.cycles++;
                    return true;
                case 0x59:
                    // LD E, C
                    this.regE = this.regC;
                    this.cycles++;
                    return true;
                case 0x5a:
                    // LD E, D
                    this.regE = this.regD;
                    this.cycles++;
                    return true;
                case 0x5b:
                    // LD E, E
                    // this.regE = this.regE;
                    this.cycles++;
                    return true;
                case 0x5c:
                    // LD E, H
                    this.regE = this.regH;
                    this.cycles++;
                    return true;
                case 0x5d:
                    // LD E, L
                    this.regE = this.regL;
                    this.cycles++;
                    return true;
                case 0x5e:
                    // LD E, (HL)
                    addr = this.RegisterHL;
                    this.regE = ReadMem(addr);
                    this.cycles++;
                    return true;
                case 0x5f:
                    // LD E, A
                    this.regE = this.regA;
                    this.cycles++;
                    return true;

                case 0x60:
                    // LD H, B
                    this.regH = this.regB;
                    this.cycles++;
                    return true;
                case 0x61:
                    // LD H, C
                    this.regH = this.regC;
                    this.cycles++;
                    return true;
                case 0x62:
                    // LD H, D
                    this.regH = this.regD;
                    this.cycles++;
                    return true;
                case 0x63:
                    // LD H, E
                    this.regH = this.regE;
                    this.cycles++;
                    return true;
                case 0x64:
                    // LD H, H
                    // this.regH = this.regH;
                    this.cycles++;
                    return true;
                case 0x65:
                    // LD H, L
                    this.regH = this.regL;
                    this.cycles++;
                    return true;
                case 0x66:
                    // LD H, (HL)
                    addr = this.RegisterHL;
                    this.regH = ReadMem(addr);
                    this.cycles++;
                    return true;
                case 0x67:
                    // LD H, A
                    this.regH = this.regA;
                    this.cycles++;
                    return true;
                case 0x68:
                    // LD L, B
                    this.regL = this.regB;
                    this.cycles++;
                    return true;
                case 0x69:
                    // LD L, C
                    this.regL = this.regC;
                    this.cycles++;
                    return true;
                case 0x6a:
                    // LD L, D
                    this.regL = this.regD;
                    this.cycles++;
                    return true;
                case 0x6b:
                    // LD L, E
                    this.regL = this.regE;
                    this.cycles++;
                    return true;
                case 0x6c:
                    // LD L, H
                    this.regL = this.regH;
                    this.cycles++;
                    return true;
                case 0x6d:
                    // LD L, L
                    // this.regL = this.regL;
                    this.cycles++;
                    return true;
                case 0x6e:
                    // LD L, (HL)
                    addr = this.RegisterHL;
                    this.regL = ReadMem(addr);
                    this.cycles++;
                    return true;
                case 0x6f:
                    // LD L, A
                    this.regL = this.regA;
                    this.cycles++;
                    return true;

                case 0x70:
                    // LD (HL), B
                    addr = this.RegisterHL;
                    WriteMem(addr, this.regB);
                    this.cycles++;
                    return true;
                case 0x71:
                    // LD (HL), C
                    addr = this.RegisterHL;
                    WriteMem(addr, this.regC);
                    this.cycles++;
                    return true;
                case 0x72:
                    // LD (HL), D
                    addr = this.RegisterHL;
                    WriteMem(addr, this.regD);
                    this.cycles++;
                    return true;
                case 0x73:
                    // LD (HL), E
                    addr = this.RegisterHL;
                    WriteMem(addr, this.regE);
                    this.cycles++;
                    return true;
                case 0x74:
                    // LD (HL), H
                    addr = this.RegisterHL;
                    WriteMem(addr, this.regH);
                    this.cycles++;
                    return true;
                case 0x75:
                    // LD (HL), L
                    addr = this.RegisterHL;
                    WriteMem(addr, this.regL);
                    this.cycles++;
                    return true;
                case 0x76:
                    // HALT
                    InstrHALT();
                    return true;
                case 0x77:
                    // LD (HL), A
                    addr = this.RegisterHL;
                    WriteMem(addr, this.regA);
                    this.cycles++;
                    return true;
                case 0x78:
                    // LD A, B
                    this.regA = this.regB;
                    this.cycles++;
                    return true;
                case 0x79:
                    // LD A, C
                    this.regA = this.regC;
                    this.cycles++;
                    return true;
                case 0x7a:
                    // LD A, D
                    this.regA = this.regD;
                    this.cycles++;
                    return true;
                case 0x7b:
                    // LD A, E
                    this.regA = this.regE;
                    this.cycles++;
                    return true;
                case 0x7c:
                    // LD A, H
                    this.regA = this.regH;
                    this.cycles++;
                    return true;
                case 0x7d:
                    // LD A, L
                    this.regA = this.regL;
                    this.cycles++;
                    return true;
                case 0x7e:
                    // LD A, (HL)
                    addr = this.RegisterHL;
                    this.regA = ReadMem(addr);
                    this.cycles++;
                    return true;
                case 0x7f:
                    // LD A, A
                    // this.regA = this.regA;
                    this.cycles++;
                    return true;

                case 0x80:
                    // ADD A, B
                    this.regA = Do8bitAdd(this.regA, this.regB, false);
                    this.cycles++;
                    return true;
                case 0x81:
                    // ADD A, C
                    this.regA = Do8bitAdd(this.regA, this.regC, false);
                    this.cycles++;
                    return true;
                case 0x82:
                    // ADD A, D
                    this.regA = Do8bitAdd(this.regA, this.regD, false);
                    this.cycles++;
                    return true;
                case 0x83:
                    // ADD A, E
                    this.regA = Do8bitAdd(this.regA, this.regE, false);
                    this.cycles++;
                    return true;
                case 0x84:
                    // ADD A, H
                    this.regA = Do8bitAdd(this.regA, this.regH, false);
                    this.cycles++;
                    return true;
                case 0x85:
                    // ADD A, L
                    this.regA = Do8bitAdd(this.regA, this.regL, false);
                    this.cycles++;
                    return true;
                case 0x86:
                    // ADD A, (HL)
                    addr = this.RegisterHL;
                    val8 = ReadMem(addr);
                    this.regA = Do8bitAdd(this.regA, val8, false);
                    this.cycles++;
                    return true;
                case 0x87:
                    // ADD A, A
                    this.regA = Do8bitAdd(this.regA, this.regA, false);
                    this.cycles++;
                    return true;
                case 0x88:
                    // ADC A, B
                    this.regA = Do8bitAdd(this.regA, this.regB, true);
                    this.cycles++;
                    return true;
                case 0x89:
                    // ADC A, C
                    this.regA = Do8bitAdd(this.regA, this.regC, true);
                    this.cycles++;
                    return true;
                case 0x8a:
                    // ADC A, D
                    this.regA = Do8bitAdd(this.regA, this.regD, true);
                    this.cycles++;
                    return true;
                case 0x8b:
                    // ADC A, E
                    this.regA = Do8bitAdd(this.regA, this.regE, true);
                    this.cycles++;
                    return true;
                case 0x8c:
                    // ADC A, H
                    this.regA = Do8bitAdd(this.regA, this.regH, true);
                    this.cycles++;
                    return true;
                case 0x8d:
                    // ADC A, L
                    this.regA = Do8bitAdd(this.regA, this.regL, true);
                    this.cycles++;
                    return true;
                case 0x8e:
                    // ADC A, (HL)
                    addr = this.RegisterHL;
                    val8 = ReadMem(addr);
                    this.regA = Do8bitAdd(this.regA, val8, true);
                    this.cycles++;
                    return true;
                case 0x8f:
                    // ADC A, A
                    this.regA = Do8bitAdd(this.regA, this.regA, true);
                    this.cycles++;
                    return true;

                case 0x90:
                    // SUB A, B
                    this.regA = Do8bitSub(this.regA, this.regB, false);
                    this.cycles++;
                    return true;
                case 0x91:
                    // SUB A, C
                    this.regA = Do8bitSub(this.regA, this.regC, false);
                    this.cycles++;
                    return true;
                case 0x92:
                    // SUB A, D
                    this.regA = Do8bitSub(this.regA, this.regD, false);
                    this.cycles++;
                    return true;
                case 0x93:
                    // SUB A, E
                    this.regA = Do8bitSub(this.regA, this.regE, false);
                    this.cycles++;
                    return true;
                case 0x94:
                    // SUB A, H
                    this.regA = Do8bitSub(this.regA, this.regH, false);
                    this.cycles++;
                    return true;
                case 0x95:
                    // SUB A, L
                    this.regA = Do8bitSub(this.regA, this.regL, false);
                    this.cycles++;
                    return true;
                case 0x96:
                    // SUB A, (HL)
                    addr = this.RegisterHL;
                    val8 = ReadMem(addr);
                    this.regA = Do8bitSub(this.regA, val8, false);
                    this.cycles++;
                    return true;
                case 0x97:
                    // SUB A, A
                    this.regA = Do8bitSub(this.regA, this.regA, false);
                    this.cycles++;
                    return true;
                case 0x98:
                    // SBC A, B
                    this.regA = Do8bitSub(this.regA, this.regB, true);
                    this.cycles++;
                    return true;
                case 0x99:
                    // SBC A, C
                    this.regA = Do8bitSub(this.regA, this.regC, true);
                    this.cycles++;
                    return true;
                case 0x9a:
                    // SBC A, D
                    this.regA = Do8bitSub(this.regA, this.regD, true);
                    this.cycles++;
                    return true;
                case 0x9b:
                    // SBC A, E
                    this.regA = Do8bitSub(this.regA, this.regE, true);
                    this.cycles++;
                    return true;
                case 0x9c:
                    // SBC A, H
                    this.regA = Do8bitSub(this.regA, this.regH, true);
                    this.cycles++;
                    return true;
                case 0x9d:
                    // SBC A, L
                    this.regA = Do8bitSub(this.regA, this.regL, true);
                    this.cycles++;
                    return true;
                case 0x9e:
                    // SBC A, (HL)
                    addr = this.RegisterHL;
                    val8 = ReadMem(addr);
                    this.regA = Do8bitSub(this.regA, val8, true);
                    this.cycles++;
                    return true;
                case 0x9f:
                    // SBC A, A
                    this.regA = Do8bitSub(this.regA, this.regA, true);
                    this.cycles++;
                    return true;

                case 0xa0:
                    // AND A, B
                    InstrANDA(this.regB);
                    return true;
                case 0xa1:
                    // AND A, C
                    InstrANDA(this.regC);
                    return true;
                case 0xa2:
                    // AND A, D
                    InstrANDA(this.regD);
                    return true;
                case 0xa3:
                    // AND A, E
                    InstrANDA(this.regE);
                    return true;
                case 0xa4:
                    // AND A, H
                    InstrANDA(this.regH);
                    return true;
                case 0xa5:
                    // AND A, L
                    InstrANDA(this.regL);
                    return true;
                case 0xa6:
                    // AND A, (HL)
                    addr = this.RegisterHL;
                    val8 = ReadMem(addr);
                    InstrANDA(val8);
                    return true;
                case 0xa7:
                    // AND A, A
                    InstrANDA(this.regA);
                    return true;
                case 0xa8:
                    // XOR A, B
                    InstrXORA(this.regB);
                    return true;
                case 0xa9:
                    // XOR A, C
                    InstrXORA(this.regC);
                    return true;
                case 0xaa:
                    // XOR A, D
                    InstrXORA(this.regD);
                    return true;
                case 0xab:
                    // XOR A, E
                    InstrXORA(this.regE);
                    return true;
                case 0xac:
                    // XOR A, H
                    InstrXORA(this.regH);
                    return true;
                case 0xad:
                    // XOR A, L
                    InstrXORA(this.regL);
                    return true;
                case 0xae:
                    // XOR A, (HL)
                    addr = this.RegisterHL;
                    val8 = ReadMem(addr);
                    InstrXORA(val8);
                    return true;
                case 0xaf:
                    // XOR A, A
                    InstrXORA(this.regA);
                    return true;

                case 0xb0:
                    // OR A, B
                    InstrORA(this.regB);
                    return true;
                case 0xb1:
                    // OR A, C
                    InstrORA(this.regC);
                    return true;
                case 0xb2:
                    // OR A, D
                    InstrORA(this.regD);
                    return true;
                case 0xb3:
                    // OR A, E
                    InstrORA(this.regE);
                    return true;
                case 0xb4:
                    // OR A, H
                    InstrORA(this.regH);
                    return true;
                case 0xb5:
                    // OR A, L
                    InstrORA(this.regL);
                    return true;
                case 0xb6:
                    // OR A, (HL)
                    addr = this.RegisterHL;
                    val8 = ReadMem(addr);
                    InstrORA(val8);
                    return true;
                case 0xb7:
                    // OR A, A
                    InstrORA(this.regA);
                    return true;
                case 0xb8:
                    // CP A, B
                    Do8bitSub(this.regA, this.regB, false);
                    this.cycles++;
                    return true;
                case 0xb9:
                    // CP A, C
                    Do8bitSub(this.regA, this.regC, false);
                    this.cycles++;
                    return true;
                case 0xba:
                    // CP A, D
                    Do8bitSub(this.regA, this.regD, false);
                    this.cycles++;
                    return true;
                case 0xbb:
                    // CP A, E
                    Do8bitSub(this.regA, this.regE, false);
                    this.cycles++;
                    return true;
                case 0xbc:
                    // CP A, H
                    Do8bitSub(this.regA, this.regH, false);
                    this.cycles++;
                    return true;
                case 0xbd:
                    // CP A, L
                    Do8bitSub(this.regA, this.regL, false);
                    this.cycles++;
                    return true;
                case 0xbe:
                    // CP A, (HL)
                    addr = this.RegisterHL;
                    val8 = ReadMem(addr);
                    Do8bitSub(this.regA, val8, false);
                    this.cycles++;
                    return true;
                case 0xbf:
                    // CP A, A
                    Do8bitSub(this.regA, this.regA, false);
                    this.cycles++;
                    return true;

                case 0xc0:
                    // RET NZ
                    InstrRET(!(this.flagZ));
                    return true;
                case 0xc1:
                    // POP BC
                    this.RegisterBC = InstrPOP();
                    return true;
                case 0xc2:
                    // JP NZ, nnnn
                    InstrJP(!(this.flagZ));
                    return true;
                case 0xc3:
                    // JP nnnn
                    InstrJP(true);
                    return true;
                case 0xc4:
                    // CALL NZ, nnnn
                    InstrCALL(!(this.flagZ));
                    return true;
                case 0xc5:
                    // PUSH BC
                    InstrPUSH(this.RegisterBC);
                    return true;
                case 0xc6:
                    // ADD A, #nn
                    val8 = AddrModeImmediateValue();
                    this.regA = Do8bitAdd(this.regA, val8, false);
                    this.cycles++;
                    return true;
                case 0xc7:
                    // RST 0000h
                    InstrRST(0);
                    return true;
                case 0xc8:
                    // RET Z
                    InstrRET(this.flagZ);
                    return true;
                case 0xc9:
                    // RET
                    InstrRET(true);
                    this.cycles--;
                    return true;
                case 0xca:
                    // JP Z, nnnn
                    InstrJP(this.flagZ);
                    return true;

                /* ~~ Opcodes en CB gérés par une autre méthode ! ~~ */

                case 0xcc:
                    // CALL Z, nnnn
                    InstrCALL(this.flagZ);
                    return true;
                case 0xcd:
                    // CALL nnnn
                    InstrCALL(true);
                    return true;
                case 0xce:
                    // ADC A, #nn
                    val8 = AddrModeImmediateValue();
                    this.regA = Do8bitAdd(this.regA, val8, true);
                    this.cycles++;
                    return true;
                case 0xcf:
                    // RST 0008h
                    InstrRST(1);
                    return true;

                case 0xd0:
                    // RET NC
                    InstrRET(!(this.flagC));
                    return true;
                case 0xd1:
                    // POP DE
                    this.RegisterDE = InstrPOP();
                    return true;
                case 0xd2:
                    // JP NC, nnnn
                    InstrJP(!(this.flagC));
                    return true;
                case 0xd3:
                    // OUT (nn), A
                    port = AddrModeImmediateValue();
                    InstrOUT(port, this.regA);
                    return true;
                case 0xd4:
                    // CALL NC, nnnn
                    InstrCALL(!(this.flagC));
                    return true;
                case 0xd5:
                    // PUSH DE
                    InstrPUSH(this.RegisterDE);
                    return true;
                case 0xd6:
                    // SUB A, #nn
                    val8 = AddrModeImmediateValue();
                    this.regA = Do8bitSub(this.regA, val8, false);
                    this.cycles++;
                    return true;
                case 0xd7:
                    // RST 0010h
                    InstrRST(2);
                    return true;
                case 0xd8:
                    // RET C
                    InstrRET(this.flagC);
                    return true;
                case 0xd9:
                    // EXX
                    InstrEXX();
                    return true;
                case 0xda:
                    // JP C, nnnn
                    InstrJP(this.flagC);
                    return true;
                case 0xdb:
                    // IN A, (nn)
                    port = AddrModeImmediateValue();
                    this.regA = InstrIN(port);
                    return true;
                case 0xdc:
                    // CALL C, nnnn
                    InstrCALL(this.flagC);
                    return true;

                /* ~~ Opcodes en DD gérés par une autre méthode ! ~~ */

                case 0xde:
                    // SBC A, #nn
                    val8 = AddrModeImmediateValue();
                    this.regA = Do8bitSub(this.regA, val8, true);
                    this.cycles++;
                    return true;
                case 0xdf:
                    // RST 0018h
                    InstrRST(3);
                    return true;

                case 0xe0:
                    // RET PO
                    InstrRET(!(this.flagPV));
                    return true;
                case 0xe1:
                    // POP HL
                    this.RegisterHL = InstrPOP();
                    return true;
                case 0xe2:
                    // JP PO, nnnn
                    InstrJP(!(this.flagPV));
                    return true;
                case 0xe3:
                    // EX (SP), HL
                    this.RegisterHL = InstrEXSP(this.RegisterHL);
                    return true;
                case 0xe4:
                    // CALL PO, nnnn
                    InstrCALL(!(this.flagPV));
                    return true;
                case 0xe5:
                    // PUSH HL
                    InstrPUSH(this.RegisterHL);
                    return true;
                case 0xe6:
                    // AND A, #nn
                    val8 = AddrModeImmediateValue();
                    InstrANDA(val8);
                    return true;
                case 0xe7:
                    // RST 0020h
                    InstrRST(4);
                    return true;
                case 0xe8:
                    // RET PE
                    InstrRET(this.flagPV);
                    return true;
                case 0xe9:
                    // JP (HL)
                    addr = this.RegisterHL; ///////////////////////////////////////////////////////////////////////////////////////
                    this.regPC = addr;
                    this.cycles++;
                    return true;
                case 0xea:
                    // JP PE, nnnn
                    InstrJP(this.flagPV);
                    return true;
                case 0xeb:
                    // EX DE, HL
                    InstrEXDEHL();
                    return true;
                case 0xec:
                    // CALL PE, nnnn
                    InstrCALL(this.flagPV);
                    return true;

                /* ~~ Opcodes en ED gérés par une autre méthode ! ~~ */

                case 0xee:
                    // XOR A, #nn
                    val8 = AddrModeImmediateValue();
                    InstrXORA(val8);
                    return true;
                case 0xef:
                    // RST 0028h
                    InstrRST(5);
                    return true;

                case 0xf0:
                    // RET PL
                    InstrRET(!(this.flagS));
                    return true;
                case 0xf1:
                    // POP AF
                    this.RegisterAF = InstrPOP();
                    return true;
                case 0xf2:
                    // JP PL, nnnn
                    InstrJP(!(this.flagS));
                    return true;
                case 0xf3:
                    // DI
                    InstrDI();
                    return true;
                case 0xf4:
                    // CALL PL, nnnn
                    InstrCALL(!(this.flagS));
                    return true;
                case 0xf5:
                    // PUSH AF
                    InstrPUSH(this.RegisterAF);
                    return true;
                case 0xf6:
                    // OR A, #nn
                    val8 = AddrModeImmediateValue();
                    InstrORA(val8);
                    return true;
                case 0xf7:
                    // RST 0030h
                    InstrRST(6);
                    return true;
                case 0xf8:
                    // RET MI
                    InstrRET(this.flagS);
                    return true;
                case 0xf9:
                    // LD SP, HL
                    this.regSP = this.RegisterHL;
                    this.cycles += 3L;
                    return true;
                case 0xfa:
                    // JP MI, nnnn
                    InstrJP(this.flagS);
                    return true;
                case 0xfb:
                    InstrEI();
                    return true;
                case 0xfc:
                    // CALL MI, nnnn
                    InstrCALL(this.flagS);
                    return true;

                /* ~~ Opcodes en FD gérés par une autre méthode ! ~~ */

                case 0xfe:
                    // CP A, #nn
                    val8 = AddrModeImmediateValue();
                    Do8bitSub(this.regA, val8, false);
                    this.cycles++;
                    return true;
                case 0xff:
                    // RST 0038h
                    InstrRST(7);
                    return true;
            }

            return false;
        }

        /* ~~~~ traçage ~~~~ */

        private void DoTrace()
        {
            this.traceFile.WriteLine(
                    "=> PC={0:X4}h SP={1:X4}h" +
                    " IX={2:X4}h IY={3:X4}h\n" +
                    "   A={4:X2}h" +
                    " B={5:X2}h C={6:X2}h" +
                    " D={7:X4}h E={8:X4}h" +
                    " H={9:X4}h L={10:X4}h\n" +
                    "   F={11:X2}h" +
                    " (S={12} Z={13} H={14} P/V={15} N={16} C={17})\n" +
                    "   A'={:X2}h F'={:X2}h" +
                    " B'={:X2}h C'={:X2}h" +
                    " D'={:X2}h E'={:X2}h" +
                    " H'={:X2}h L'={:X2}h\n" +
                    " I={:X2}h R={:X2}h",
                    this.regPC, this.regSP,
                    this.regIX, this.regIY,
                    this.regA,
                    this.regB, this.regC,
                    this.regD, this.regE,
                    this.regH, this.regL,
                    this.RegisterF,
                    (this.flagS  ? 1 : 0),
                    (this.flagZ  ? 1 : 0),
                    (this.flagH  ? 1 : 0),
                    (this.flagPV ? 1 : 0),
                    (this.flagN  ? 1 : 0),
                    (this.flagC  ? 1 : 0),
                    this.regAprime, this.regFprime,
                    this.regBprime, this.regCprime,
                    this.regDprime, this.regEprime,
                    this.regHprime, this.regLprime,
                    this.regI, this.regR);
        }


        /* ======================= MÉTHODES PUBLIQUES ======================= */

        /// <summary>
        /// Réinitialise le processeur.
        /// </summary>
        /// <exception cref="AddressUnreadableException">
        /// Si une adresse-mémoire (vecteur RESET ou sa cible)
        /// ne peut pas être lue.
        /// </exception>
        public void Reset()
        {
            // débloque le processeur
            this.halted = false;
            // met à 0 le compteur de cycles écoulés
            this.cycles = 0;
            // désactive toute interruption masquable
            this.iff1 = this.iff2 = false;
            // réinitialise la valeur du registre PC
            this.regPC = RESET_PC_ADDRESS;
            // RàZ des registres I et R
            this.regI = 0x00;
            this.regR = 0x00;
            // mode d'interruption par défaut
            this.irqMode = Z80_IntrMode.MODE_0;
            // traçage si besoin est
            if (this.traceFile != null) {
                this.traceFile.WriteLine("\n\n*** RESET! ***\n");
                DoTrace();
            }
        }

        /// <summary>
        /// Lance une interruption matérielle non-masquable (NMI).
        /// </summary>
        /// <exception cref="AddressUnreadableException">
        /// Si une adresse-mémoire (vecteur NMI ou sa cible)
        /// ne peut pas être lue.
        /// </exception>
        public void TriggerNMI()
        {
            this.nmiTrig = true;
        }

        /// <summary>
        /// Exécute l'instruction actuellement pointée par le registre PC.
        /// </summary>
        /// <returns>
        /// Nombre de cycles écoulés pour l'exécution de l'instruction.
        /// </returns>
        /// <exception cref="AddressUnreadableException">
        /// Si le contenu d'une adresse-mémoire nécessaire au travail
        /// du processeur ne peut pas être lu.
        /// </exception>
        public ulong Step()
        {
            ulong cycBegin = this.cycles;

            // la ligne reset empêche le processeur de travailler
            if (this.resetLine) return 0L;

            // l'état stoppé force le processeur à ne rien faire
            if (this.halted) {
                // équivalent de l'instruction NOP
                this.cycles += 4;
                return 4L;
            }

            // une interruption est-elle signalée ?
            if (this.nmiTrig) {
                // NMI : sensible à la transition
                this.nmiTrig = false;
                if (this.traceFile != null) {
                    this.traceFile.WriteLine("*** NMI! ***");
                }
                // débloque le processeur
                this.halted = false;
                // lance la réponse à l'interruption
                this.cycles += 2;
                // désactive toute interruption masquable
                this.iff1 = false;
                // saute au sous-programme de réponse aux NMI
                PushWord(this.regPC);
                this.regPC = NMI_PC_ADDRESS;
            } else if (this.intLine) {
                // interruption masquable
                if (this.iff1) {
                    if (this.traceFile != null) {
                        this.traceFile.WriteLine("*** IRQ! ***");
                    }
                    // débloque le processeur
                    this.halted = false;
                    // lance la réponse à l'interruption
                    this.cycles += 2;
                    // enregistre le PC actuel
                    PushWord(this.regPC);
                    // désactive toute autre interruption masquable
                    this.iff1 = this.iff2 = false;
                    // en fonction du mode IRQ actuel...
                    switch (this.irqMode) {
                        case Z80_IntrMode.MODE_0:
                            // opcode = databus;
                            // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                            break;
                        case Z80_IntrMode.MODE_1:
                            this.regPC = IRQ_PC_ADDRESS;
                            break;
                        case Z80_IntrMode.MODE_2:
                            // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                            this.regPC = MakeWord(this.regI, /* databus */0);
                            break;
                    }
                }
            }

            // désassemblage si traçage
            if (this.traceFile != null) {
                this.traceFile.Write(
                        this.traceDisasm.DisassembleInstructionAt(this.regPC));
            }

            // lit, décode et exécute le prochain opcode
            bool ok;
            byte opcode = ReadMem(this.regPC);
            this.cycles++;
            this.regPC++;
            switch (opcode) {
                case 0xcb:
                    // opcodes de rotations, décalages
                    // et autres manipulations bit-à-bit
                    opcode = ReadMem(this.regPC);
                    this.regPC++;
                    ok = ExecCBopcode(opcode);
                    break;
                case 0xdd:
                    // opcodes sur le registre d'index IX
                    opcode = ReadMem(this.regPC);
                    this.regPC++;
                    ok = ExecDDopcode(opcode);
                    break;
                case 0xed:
                    // opcodes spéciaux
                    opcode = ReadMem(this.regPC);
                    this.regPC++;
                    ok = ExecEDopcode(opcode);
                    break;
                case 0xfd:
                    // opcodes sur le registre d'index IY
                    opcode = ReadMem(this.regPC);
                    this.regPC++;
                    ok = ExecFDopcode(opcode);
                    break;
                default:
                    // opcodes sur un seul octet (par défaut)
                    ok = ExecStandardOpcode(opcode);
                    break;
            }

            // opcode invalide !
            if (!ok) {
                switch (this.uoPolicy) {
                    case UnknownOpcodePolicy.ThrowException:
                        throw new UnknownOpcodeException(
                                this.regPC,
                                opcode,
                                String.Format(ERR_UNKNOWN_OPCODE,
                                              this.regPC, opcode));
                    case UnknownOpcodePolicy.DoNop:
                        InstrNOP();
                        break;
                }
            }

            // traçage de l'exécution si besoin est
            if (this.traceFile != null) {
                DoTrace();
            }

            // comptage des cycles écoulés
            ulong cycEnd = this.cycles;
            return cycEnd - cycBegin;
        }

        /// <summary>
        /// Lance l'exécution du processeur pendant AU MOINS
        /// le nombre de cycles passé en paramètre.
        /// <br/>
        /// En effet : toute instruction entamée est terminée
        /// (y compris les éventuelles réponses aux interruptions).
        /// Ainsi, le nombre de cycles exécutés peut être égal ou
        /// supérieur au nombre voulu.
        /// </summary>
        /// <param name="numCycles">
        /// Nombre de cycles processeur à exécuter.
        /// </param>
        /// <returns>
        /// Le nombre de cycles processeur réellement exécutés.
        /// </returns>
        public ulong Run(ulong numCycles)
        {
            ulong cycCount = 0L;

            while (cycCount < numCycles) {
                cycCount += Step();
            }

            return cycCount;
        }


        /* ====================== PROPRIÉTÉS PUBLIQUES ====================== */

        /// <summary>
        /// Objet espace-mémoire attaché à ce processeur lors de sa création.
        /// (Propriété en lecture seule.)
        /// </summary>
        public IMemorySpace_Z80 MemorySpace
        {
            get { return this.memSpace; }
        }


        /// <summary>
        /// Accès au registre A (Accumulateur) du processeur.
        /// </summary>
        public Byte RegisterA
        {
            get { return this.regA; }
            set { this.regA = value; }
        }

        /// <summary>
        /// Accès au registre B du processeur.
        /// </summary>
        public Byte RegisterB
        {
            get { return this.regB; }
            set { this.regB = value; }
        }

        /// <summary>
        /// Accès au registre C du processeur.
        /// </summary>
        public Byte RegisterC
        {
            get { return this.regC; }
            set { this.regC = value; }
        }

        /// <summary>
        /// Accès au registre D du processeur.
        /// </summary>
        public Byte RegisterD
        {
            get { return this.regD; }
            set { this.regD = value; }
        }

        /// <summary>
        /// Accès au registre E du processeur.
        /// </summary>
        public Byte RegisterE
        {
            get { return this.regE; }
            set { this.regE = value; }
        }

        /// <summary>
        /// Accès au registre H du processeur.
        /// </summary>
        public Byte RegisterH
        {
            get { return this.regH; }
            set { this.regH = value; }
        }

        /// <summary>
        /// Accès au registre L du processeur.
        /// </summary>
        public Byte RegisterL
        {
            get { return this.regL; }
            set { this.regL = value; }
        }

        /// <summary>
        /// Accès au registre A' (Accumulateur de rechange) du processeur.
        /// </summary>
        public Byte RegisterAprime
        {
            get { return this.regAprime; }
            set { this.regAprime = value; }
        }

        /// <summary>
        /// Accès au registre B' du processeur.
        /// </summary>
        public Byte RegisterBprime
        {
            get { return this.regBprime; }
            set { this.regBprime = value; }
        }

        /// <summary>
        /// Accès au registre C' du processeur.
        /// </summary>
        public Byte RegisterCprime
        {
            get { return this.regCprime; }
            set { this.regCprime = value; }
        }

        /// <summary>
        /// Accès au registre D' du processeur.
        /// </summary>
        public Byte RegisterDprime
        {
            get { return this.regDprime; }
            set { this.regDprime = value; }
        }

        /// <summary>
        /// Accès au registre E' du processeur.
        /// </summary>
        public Byte RegisterEprime
        {
            get { return this.regEprime; }
            set { this.regEprime = value; }
        }

        /// <summary>
        /// Accès au registre F' du processeur.
        /// </summary>
        public Byte RegisterFprime
        {
            get { return this.regFprime; }
            set { this.regFprime = value; }
        }

        /// <summary>
        /// Accès au registre H' du processeur.
        /// </summary>
        public Byte RegisterHprime
        {
            get { return this.regHprime; }
            set { this.regHprime = value; }
        }

        /// <summary>
        /// Accès au registre L' du processeur.
        /// </summary>
        public Byte RegisterLprime
        {
            get { return this.regLprime; }
            set { this.regLprime = value; }
        }

        /// <summary>
        /// Accès au registre d'index IX du processeur.
        /// </summary>
        public UInt16 RegisterIX
        {
            get { return this.regIX; }
            set { this.regIX = value; }
        }

        /// <summary>
        /// Accès au registre d'index IY du processeur.
        /// </summary>
        public UInt16 RegisterIY
        {
            get { return this.regIY; }
            set { this.regIY = value; }
        }

        /// <summary>
        /// Accès au registre SP ("Stack Pointer",
        /// pointeur de pile) du processeur.
        /// </summary>
        public UInt16 RegisterSP
        {
            get { return this.regSP; }
            set { this.regSP = value; }
        }

        /// <summary>
        /// Accès au registre PC ("Program Counter", compteur programme
        /// alias compteur ordinal) du processeur.
        /// </summary>
        public UInt16 RegisterPC
        {
            get { return this.regPC; }
            set { this.regPC = value; }
        }

        /// <summary>
        /// Accès au registre F ("Flags", registre de statut)
        /// du processeur.
        /// </summary>
        public Byte RegisterF
        {
            get {
                byte f = 0x00;
                if (this.flagS)   f |= FLAG_S;
                if (this.flagZ)   f |= FLAG_Z;
                if (this.flagH)   f |= FLAG_H;
                if (this.flagPV)  f |= FLAG_PV;
                if (this.flagN)   f |= FLAG_N;
                if (this.flagC)   f |= FLAG_C;
                return f;
            }
            set {
                this.flagS  = ((value & FLAG_S) != 0);
                this.flagZ  = ((value & FLAG_Z) != 0);
                this.flagH  = ((value & FLAG_H) != 0);
                this.flagPV = ((value & FLAG_PV) != 0);
                this.flagN  = ((value & FLAG_N) != 0);
                this.flagC  = ((value & FLAG_C) != 0);
            }
        }

        /// <summary>
        /// Flag C ("Carry", retenue) dans le registre de statut du processeur.
        /// </summary>
        public Boolean FlagC
        {
            get { return this.flagC; }
            set { this.flagC = value; }
        }

        /// <summary>
        /// Flag N (différence addition / soustraction pour le BCD)
        /// dans le registre de statut du processeur.
        /// </summary>
        public Boolean FlagN
        {
            get { return this.flagN; }
            set { this.flagN = value; }
        }

        /// <summary>
        /// Flag P/V ("Parity/oVerflow", parité / débordement)
        /// dans le registre de statut du processeur.
        /// </summary>
        public Boolean FlagPV
        {
            get { return this.flagPV; }
            set { this.flagPV = value; }
        }

        /// <summary>
        /// Flag H ("Half-carry", demi-retenue) dans le registre de
        /// statut du processeur.
        /// </summary>
        public Boolean FlagH
        {
            get { return this.flagH; }
            set { this.flagH = value; }
        }

        /// <summary>
        /// Flag Z (Zéro) dans le registre de statut du processeur.
        /// </summary>
        public Boolean FlagZ
        {
            get { return this.flagZ; }
            set { this.flagZ = value; }
        }

        /// <summary>
        /// Flag S (Signe) dans le registre de statut du processeur.
        /// </summary>
        public Boolean FlagS
        {
            get { return this.flagS; }
            set { this.flagS = value; }
        }

        /// <summary>
        /// Flip-flop principal d'activation des interruptions masquables.
        /// </summary>
        public Boolean IFF1
        {
            get { return this.iff1; }
            set { this.iff1 = value; }
        }

        /// <summary>
        /// Flip-flop de secours d'activation des interruptions masquables.
        /// </summary>
        public Boolean IFF2
        {
            get { return this.iff2; }
            set { this.iff2 = value; }
        }

        /// <summary>
        /// Accès à la paire de registres AF du processeur.
        /// </summary>
        public UInt16 RegisterAF
        {
            get { return MakeWord(this.regA, this.RegisterF); }
            set {
                this.regA = HiByte(value);
                this.RegisterF = LoByte(value);
            }
        }

        /// <summary>
        /// Accès à la paire de registres BC du processeur.
        /// </summary>
        public UInt16 RegisterBC
        {
            get { return MakeWord(this.regB, this.regC); }
            set {
                this.regB = HiByte(value);
                this.regC = LoByte(value);
            }
        }

        /// <summary>
        /// Accès à la paire de registres DE du processeur.
        /// </summary>
        public UInt16 RegisterDE
        {
            get { return MakeWord(this.regD, this.regE); }
            set {
                this.regD = HiByte(value);
                this.regE = LoByte(value);
            }
        }

        /// <summary>
        /// Accès à la paire de registres HL du processeur.
        /// </summary>
        public UInt16 RegisterHL
        {
            get { return MakeWord(this.regH, this.regL); }
            set {
                this.regH = HiByte(value);
                this.regL = LoByte(value);
            }
        }


        /// <summary>
        /// Ligne de réinitialisation du processeur.
        /// Cette ligne est sensible au niveau.
        /// </summary>
        public Boolean ResetLine
        {
            get { return this.resetLine; }
            set {
                if (value) Reset();
                this.resetLine = value;
            }
        }

        /// <summary>
        /// Ligne de requête d'interruption matérielle non-masquable.
        /// Cette ligne est sensible à la transition.
        /// </summary>
        public Boolean NMILine
        {
            get { return this.nmiLine; }
            set {
                if (value & !nmiLine) TriggerNMI();
                this.nmiLine = value;
            }
        }

        /// <summary>
        /// Ligne de requête d'interruption matérielle (masquable).
        /// Cette ligne est sensible au niveau.
        /// </summary>
        public Boolean INTLine
        {
            get { return this.intLine; }
            set { this.intLine = value; }
        }

        /// <summary>
        /// Indique si le processeur est "arrêté",
        /// suite à une instruction HALT.
        /// (Propriété en lecture seule,
        ///  utiliser une interruption ou un reset
        ///  pour relancer le processeur.)
        /// </summary>
        public Boolean IsHalted
        {
            get { return this.halted; }
        }


        /// <summary>
        /// Politique de prise en charge des opcodes invalides à l'exécution.
        /// </summary>
        public UnknownOpcodePolicy InvalidOpcodePolicy
        {
            get { return this.uoPolicy; }
            set { this.uoPolicy = value; }
        }


        /// <summary>
        /// Objet d'écriture dans le fichier de traçage
        /// à employer pour l'exécution de ce processeur.
        /// <br/>
        /// Mettre à <code>null</code> pour ne pas faire de trace.
        /// </summary>
        public StreamWriter TraceFileWriter
        {
            get { return this.traceFile; }
            set {
                if (this.traceFile != null) {
                    this.traceFile.Flush();
                }
                this.traceFile = value;
                if (this.traceFile != null) {
                    this.traceDisasm = new Disasm_Z80(this.memSpace)
                    {
                        InvalidOpcodePolicy = this.uoPolicy
                    };
                } else {
                    this.traceDisasm = null;
                }
            }
        }

    }
}

