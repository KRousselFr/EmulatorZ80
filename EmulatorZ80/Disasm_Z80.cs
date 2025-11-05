using System;
using System.Text;


namespace EmulatorZ80
{
    /// <summary>
    /// Classe désassemblant le code machine du processeur Zilog Z80.
    /// </summary>
    public class Disasm_Z80
    {
        /* =========================== CONSTANTES =========================== */

        // messages affichés
        private const String ERR_UNREADABLE_ADDRESS =
                "Impossible de lire le contenu de l'adresse ${0:X4} !";
        private const String ERR_UNKNOWN_OPCODE =
                "Opcode invalide (${1:X2}) rencontré à l'adresse ${0:X4} !";


        /* ========================== CHAMPS PRIVÉS ========================= */

        // espace-mémoire attaché au processeur
        // (défini une fois pour toutes à la construction)
        private readonly IMemorySpace_Z80 memSpace;

        // politique vis-à-vis des opcodes invalides
        private UnknownOpcodePolicy uoPolicy;

        // adresse courante de l'instruction en cours de désassemblage
        private int regPC;


        /* ========================== CONSTRUCTEUR ========================== */

        /// <summary>
        /// Constructeur de référence (et unique) de la classe Disasm_Z80.
        /// </summary>
        /// <param name="memorySpace">
        /// Espace-mémoire où lire le code binaire à desassembler.
        /// </param>
        public Disasm_Z80(IMemorySpace_Z80 memorySpace)
        {
            this.memSpace = memorySpace;
            this.uoPolicy = UnknownOpcodePolicy.DoNop;
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

        private byte ReadMem(int addr)
        {
            byte? memval = this.memSpace.ReadMemory((ushort)addr);
            if (!(memval.HasValue)) {
                throw new AddressUnreadableException(
                        addr,
                        String.Format(ERR_UNREADABLE_ADDRESS,
                                      addr));
            }
            return memval.Value;
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





        /* ~~~~ désassemblage des opcodes ~~~~ */

        // opcodes de rotations, décalages
        // et autres manipulations bit-à-bit
        private string DisasmCBopcode(byte opcode)
        {
            string mnemo = null;
            string args = String.Empty;

            switch (opcode) {
                case 0x00:
                    return "RLC B";
                case 0x01:
                    return "RLC C";
                case 0x02:
                    return "RLC D";
                case 0x03:
                    return "RLC E";
                case 0x04:
                    return "RLC H";
                case 0x05:
                    return "RLC L";
                case 0x06:
                    return "RLC (HL)";
                case 0x07:
                    return "RLC A";
                case 0x08:
                    return "RRC B";
                case 0x09:
                    return "RRC C";
                case 0x0a:
                    return "RRC D";
                case 0x0b:
                    return "RRC E";
                case 0x0c:
                    return "RRC H";
                case 0x0d:
                    return "RRC L";
                case 0x0e:
                    return "RRC (HL)";
                case 0x0f:
                    return "RRC A";

                case 0x10:
                    return "RL B";
                case 0x11:
                    return "RL C";
                case 0x12:
                    return "RL D";
                case 0x13:
                    return "RL E";
                case 0x14:
                    return "RL H";
                case 0x15:
                    return "RL L";
                case 0x16:
                    return "RL (HL)";
                case 0x17:
                    return "RL A";
                case 0x18:
                    return "RR B";
                case 0x19:
                    return "RR C";
                case 0x1a:
                    return "RR D";
                case 0x1b:
                    return "RR E";
                case 0x1c:
                    return "RR H";
                case 0x1d:
                    return "RR L";
                case 0x1e:
                    return "RR (HL)";
                case 0x1f:
                    return "RR A";

                case 0x20:
                    return "SLA B";
                case 0x21:
                    return "SLA C";
                case 0x22:
                    return "SLA D";
                case 0x23:
                    return "SLA E";
                case 0x24:
                    return "SLA H";
                case 0x25:
                    return "SLA L";
                case 0x26:
                    return "SLA (HL)";
                case 0x27:
                    return "SLA A";
                case 0x28:
                    return "SRA B";
                case 0x29:
                    return "SRA C";
                case 0x2a:
                    return "SRA D";
                case 0x2b:
                    return "SRA E";
                case 0x2c:
                    return "SRA H";
                case 0x2d:
                    return "SRA L";
                case 0x2e:
                    return "SRA (HL)";
                case 0x2f:
                    return "SRA A";

                case 0x30:
                    return "SLL B";
                case 0x31:
                    return "SLL C";
                case 0x32:
                    return "SLL D";
                case 0x33:
                    return "SLL E";
                case 0x34:
                    return "SLL H";
                case 0x35:
                    return "SLL L";
                case 0x36:
                    return "SLL (HL)";
                case 0x37:
                    return "SLL A";
                case 0x38:
                    return "SRL B";
                case 0x39:
                    return "SRL C";
                case 0x3a:
                    return "SRL D";
                case 0x3b:
                    return "SRL E";
                case 0x3c:
                    return "SRL H";
                case 0x3d:
                    return "SRL L";
                case 0x3e:
                    return "SRL (HL)";
                case 0x3f:
                    return "SRL A";
            }

            if (opcode >= 0x40) {
                if (opcode <= 0x7f) {
                    mnemo = "BIT ";
                } else if (opcode <= 0xbf) {
                    mnemo = "RES ";
                } else if (opcode <= 0xff) {
                    mnemo = "SET ";
                }
                string src = null;
                switch (opcode & 0x07) {
                    case 0: src = "B"; break;
                    case 1: src = "C"; break;
                    case 2: src = "D"; break;
                    case 3: src = "E"; break;
                    case 4: src = "H"; break;
                    case 5: src = "L"; break;
                    case 6: src = "(HL)"; break;
                    case 7: src = "A"; break;
                }
                int nbit = ((opcode & 0x38) >> 3);
                args = String.Format("{0}, {1}", nbit, src);
            }

            if (mnemo != null) {
                return String.Format("{0} {1}", mnemo, args).Trim();
            }
            return null;
        }

        // opcodes sur le registre d'index IX
        private string DisasmDDopcode(byte opcode)
        {
            string mnemo = null;
            string args = String.Empty;
            sbyte dpl;
            byte val8;
            ushort val16, addr;
            byte subOpcode;

            switch (opcode) {
                case 0x09:
                    return "ADD IX, BC";

                case 0x19:
                    return "ADD IX, DE";

                case 0x21:
                    mnemo = "LD IX, ";
                    val16 = AddrModeImmediateExtendedValue();
                    args = String.Format("#{0:X4}h", val16);
                    break;
                case 0x22:
                    mnemo = "LD ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("({0:X4}h), IX", addr);
                    break;
                case 0x23:
                    return "INC IX";
                case 0x29:
                    return "ADD IX, IX";
                case 0x2a:
                    mnemo = "LD IX, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("({0:X4}h)", addr);
                    break;
                case 0x2b:
                    return "DEC IX";

                case 0x34:
                    mnemo = "INC ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0})", dpl);
                    break;
                case 0x35:
                    mnemo = "DEC ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0})", dpl);
                    break;
                case 0x36:
                    mnemo = "LD ";
                    dpl = AddrModeIndexDisplacement();
                    val8 = AddrModeImmediateValue();
                    args = String.Format("(IX + {0}), #{0:X2}h",
                                         dpl, val8);
                    break;
                case 0x39:
                    return "ADD IX, SP";

                case 0x46:
                    mnemo = "LD B, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0})", dpl);
                    break;
                case 0x4e:
                    mnemo = "LD C, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0})", dpl);
                    break;

                case 0x56:
                    mnemo = "LD D, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0})", dpl);
                    break;
                case 0x5e:
                    mnemo = "LD E, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0})", dpl);
                    break;

                case 0x66:
                    mnemo = "LD H, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0})", dpl);
                    break;
                case 0x6e:
                    mnemo = "LD L, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0})", dpl);
                    break;

                case 0x70:
                    mnemo = "LD ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0}), B", dpl);
                    break;
                case 0x71:
                    mnemo = "LD ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0}), C", dpl);
                    break;
                case 0x72:
                    mnemo = "LD ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0}), D", dpl);
                    break;
                case 0x73:
                    mnemo = "LD ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0}), E", dpl);
                    break;
                case 0x74:
                    mnemo = "LD ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0}), H", dpl);
                    break;
                case 0x75:
                    mnemo = "LD ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0}), L", dpl);
                    break;
                case 0x77:
                    mnemo = "LD ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0}), A", dpl);
                    break;
                case 0x7e:
                    mnemo = "LD A, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0})", dpl);
                    break;

                case 0x86:
                    mnemo = "ADD A, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0})", dpl);
                    break;
                case 0x8e:
                    mnemo = "ADC A, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0})", dpl);
                    break;

                case 0x96:
                    mnemo = "SUB A, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0})", dpl);
                    break;
                case 0x9e:
                    mnemo = "SBC A, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0})", dpl);
                    break;

                case 0xa6:
                    mnemo = "AND A, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0})", dpl);
                    break;
                case 0xae:
                    mnemo = "XOR A, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0})", dpl);
                    break;

                case 0xb6:
                    mnemo = "OR A, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0})", dpl);
                    break;
                case 0xbe:
                    mnemo = "CP A, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0})", dpl);
                    break;

                case 0xcb:
                    // rotations, décalages et manipulations
                    // bit-à-bit via IX
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IX + {0})", dpl);
                    subOpcode = ReadMem(this.regPC);
                    this.regPC++;
                    switch (subOpcode) {
                        case 0x06:
                            mnemo = "RLC ";
                            break;
                        case 0x0e:
                            mnemo = "RRC ";
                            break;
                        case 0x16:
                            mnemo = "RL ";
                            break;
                        case 0x1e:
                            mnemo = "RR ";
                            break;
                        case 0x26:
                            mnemo = "SLA ";
                            break;
                        case 0x2e:
                            mnemo = "SRA ";
                            break;
                        case 0x36:
                            mnemo = "SLL ";
                            break;
                        case 0x3e:
                            mnemo = "SRL ";
                            break;
                        case 0x46:
                            mnemo = "BIT 0, ";
                            break;
                        case 0x4e:
                            mnemo = "BIT 1, ";
                            break;
                        case 0x56:
                            mnemo = "BIT 2, ";
                            break;
                        case 0x5e:
                            mnemo = "BIT 3, ";
                            break;
                        case 0x66:
                            mnemo = "BIT 4, ";
                            break;
                        case 0x6e:
                            mnemo = "BIT 5, ";
                            break;
                        case 0x76:
                            mnemo = "BIT 6, ";
                            break;
                        case 0x7e:
                            mnemo = "BIT 7, ";
                            break;
                        case 0x86:
                            mnemo = "RES 0, ";
                            break;
                        case 0x8e:
                            mnemo = "RES 1, ";
                            break;
                        case 0x96:
                            mnemo = "RES 2, ";
                            break;
                        case 0x9e:
                            mnemo = "RES 3, ";
                            break;
                        case 0xa6:
                            mnemo = "RES 4, ";
                            break;
                        case 0xae:
                            mnemo = "RES 5, ";
                            break;
                        case 0xb6:
                            mnemo = "RES 6, ";
                            break;
                        case 0xbe:
                            mnemo = "RES 7, ";
                            break;
                        case 0xc6:
                            mnemo = "SET 0, ";
                            break;
                        case 0xce:
                            mnemo = "SET 1, ";
                            break;
                        case 0xd6:
                            mnemo = "SET 2, ";
                            break;
                        case 0xde:
                            mnemo = "SET 3, ";
                            break;
                        case 0xe6:
                            mnemo = "SET 4, ";
                            break;
                        case 0xee:
                            mnemo = "SET 5, ";
                            break;
                        case 0xf6:
                            mnemo = "SET 6, ";
                            break;
                        case 0xfe:
                            mnemo = "SET 7, ";
                            break;
                    }
                    break;

                case 0xe1:
                    return "POP IX";
                case 0xe3:
                    return "EX (SP), IX";
                case 0xe5:
                    return "PUSH IX";
                case 0xe9:
                    return "JP (IX)";

                case 0xf9:
                    return "LD SP, IX";
            }

            if (mnemo != null) {
                return String.Format("{0} {1}", mnemo, args).Trim();
            }
            return null;
        }

        // opcodes spéciaux
        private string DisasmEDopcode(byte opcode)
        {
            string mnemo = null;
            string args = String.Empty;
            ushort addr;

            switch (opcode) {

                case 0x40:
                    return "IN B, (C)";
                case 0x41:
                    return "OUT (C), B";
                case 0x42:
                    return "SBC HL, BC";
                case 0x43:
                    mnemo = "LD ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("({0:X4}h), BC", addr);
                    break;
                case 0x44:
                    return "NEG A";
                case 0x45:
                    return "RETN";
                case 0x46:
                    return "IM 0";
                case 0x47:
                    return "LD I, A";
                case 0x48:
                    return "IN C, (C)";
                case 0x49:
                    return "OUT (C), C";
                case 0x4a:
                    return "ADC HL, BC";
                case 0x4b:
                    mnemo = "LD BC, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("({0:X4}h)", addr);
                    break;
                case 0x4d:
                    return "RETI";
                case 0x4f:
                    return "LD R, A";

                case 0x50:
                    return "IN D, (C)";
                case 0x51:
                    return "OUT (C), D";
                case 0x52:
                    return "SBC HL, DE";
                case 0x53:
                    mnemo = "LD ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("({0:X4}h), DE", addr);
                    break;
                case 0x56:
                    return "IM 1";
                case 0x57:
                    return "LD A, I";
                case 0x58:
                    return "IN E, (C)";
                case 0x59:
                    return "OUT (C), E";
                case 0x5a:
                    return "ADC HL, DE";
                case 0x5b:
                    mnemo = "LD DE, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("({0:X4}h)", addr);
                    break;
                case 0x5e:
                    return "IM 2";
                case 0x5f:
                    return "LD A, R";

                case 0x60:
                    return "IN H, (C)";
                case 0x61:
                    return "OUT (C), H";
                case 0x62:
                    return "SBC HL, HL";
                case 0x63:
                    mnemo = "LD ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("({0:X4}h), HL", addr);
                    break;
                case 0x67:
                    return "RRD";
                case 0x68:
                    return "IN L, (C)";
                case 0x69:
                    return "OUT (C), L";
                case 0x6a:
                    return "ADC HL, HL";
                case 0x6b:
                    mnemo = "LD HL, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("({0:X4}h)", addr);
                    break;
                case 0x6f:
                    return "RLD";

                case 0x70:
                    return "IN F, (C)";   // non officiel !
                case 0x72:
                    return "SBC HL, SP";
                case 0x73:
                    mnemo = "LD ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("({0:X4}h), SP", addr);
                    break;
                case 0x78:
                    return "IN A, (C)";
                case 0x79:
                    return "OUT (C), A";
                case 0x7a:
                    return "ADC HL, SP";
                case 0x7b:
                    mnemo = "LD SP, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("({0:X4}h)", addr);
                    break;

                case 0xa0:
                    return "LDI";
                case 0xa1:
                    return "CPI";
                case 0xa2:
                    return "INI";
                case 0xa3:
                    return "OUTI";
                case 0xa8:
                    return "LDD";
                case 0xa9:
                    return "CPD";
                case 0xaa:
                    return "IND";
                case 0xab:
                    return "OUTD";

                case 0xb0:
                    return "LDIR";
                case 0xb1:
                    return "CPIR";
                case 0xb2:
                    return "INIR";
                case 0xb3:
                    return "OTIR";
                case 0xb8:
                    return "LDDR";
                case 0xb9:
                    return "CPDR";
                case 0xba:
                    return "INDR";
                case 0xbb:
                    return "OTDR";

            }

            if (mnemo != null) {
                return String.Format("{0} {1}", mnemo, args).Trim();
            }
            return null;
        }

        // opcodes sur le registre d'index IY
        private string DisasmFDopcode(byte opcode)
        {
            string mnemo = null;
            string args = String.Empty;
            sbyte dpl;
            byte val8;
            ushort val16, addr;
            byte subOpcode;

            switch (opcode) {
                case 0x09:
                    return "ADD IY, BC";

                case 0x19:
                    return "ADD IY, DE";

                case 0x21:
                    mnemo = "LD IY, ";
                    val16 = AddrModeImmediateExtendedValue();
                    args = String.Format("#{0:X4}h", val16);
                    break;
                case 0x22:
                    mnemo = "LD ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("({0:X4}h), IY", addr);
                    break;
                case 0x23:
                    return "INC IY";
                case 0x29:
                    return "ADD IY, IY";
                case 0x2a:
                    mnemo = "LD IY, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("(#{0:X4}h)", addr);
                    break;
                case 0x2b:
                    return "DEC IY";

                case 0x34:
                    mnemo = "INC ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0})", dpl);
                    break;
                case 0x35:
                    mnemo = "DEC ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0})", dpl);
                    break;
                case 0x36:
                    mnemo = "LD ";
                    dpl = AddrModeIndexDisplacement();
                    val8 = AddrModeImmediateValue();
                    args = String.Format("(IY + {0}), #{0:X2}h",
                                         dpl, val8);
                    break;
                case 0x39:
                    return "ADD IY, SP";

                case 0x46:
                    mnemo = "LD B, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0})", dpl);
                    break;
                case 0x4e:
                    mnemo = "LD C, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0})", dpl);
                    break;

                case 0x56:
                    mnemo = "LD D, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0})", dpl);
                    break;
                case 0x5e:
                    mnemo = "LD E, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0})", dpl);
                    break;

                case 0x66:
                    mnemo = "LD H, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0})", dpl);
                    break;
                case 0x6e:
                    mnemo = "LD L, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0})", dpl);
                    break;

                case 0x70:
                    mnemo = "LD ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0}), B", dpl);
                    break;
                case 0x71:
                    mnemo = "LD ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0}), C", dpl);
                    break;
                case 0x72:
                    mnemo = "LD ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0}), D", dpl);
                    break;
                case 0x73:
                    mnemo = "LD ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0}), E", dpl);
                    break;
                case 0x74:
                    mnemo = "LD ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0}), H", dpl);
                    break;
                case 0x75:
                    mnemo = "LD ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0}), L", dpl);
                    break;
                case 0x77:
                    mnemo = "LD ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0}), A", dpl);
                    break;
                case 0x7e:
                    mnemo = "LD A, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0})", dpl);
                    break;

                case 0x86:
                    mnemo = "ADD A, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0})", dpl);
                    break;
                case 0x8e:
                    mnemo = "ADC A, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0})", dpl);
                    break;

                case 0x96:
                    mnemo = "SUB A, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0})", dpl);
                    break;
                case 0x9e:
                    mnemo = "SBC A, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0})", dpl);
                    break;

                case 0xa6:
                    mnemo = "AND A, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0})", dpl);
                    break;
                case 0xae:
                    mnemo = "XOR A, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0})", dpl);
                    break;

                case 0xb6:
                    mnemo = "OR A, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0})", dpl);
                    break;
                case 0xbe:
                    mnemo = "CP A, ";
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0})", dpl);
                    break;

                case 0xcb:
                    // rotations, décalages et manipulations
                    // bit-à-bit via IY
                    dpl = AddrModeIndexDisplacement();
                    args = String.Format("(IY + {0})", dpl);
                    subOpcode = ReadMem(this.regPC);
                    this.regPC++;
                    switch (subOpcode) {
                        case 0x06:
                            mnemo = "RLC ";
                            break;
                        case 0x0e:
                            mnemo = "RRC ";
                            break;
                        case 0x16:
                            mnemo = "RL ";
                            break;
                        case 0x1e:
                            mnemo = "RR ";
                            break;
                        case 0x26:
                            mnemo = "SLA ";
                            break;
                        case 0x2e:
                            mnemo = "SRA ";
                            break;
                        case 0x36:
                            mnemo = "SLL ";
                            break;
                        case 0x3e:
                            mnemo = "SRL ";
                            break;
                        case 0x46:
                            mnemo = "BIT 0, ";
                            break;
                        case 0x4e:
                            mnemo = "BIT 1, ";
                            break;
                        case 0x56:
                            mnemo = "BIT 2, ";
                            break;
                        case 0x5e:
                            mnemo = "BIT 3, ";
                            break;
                        case 0x66:
                            mnemo = "BIT 4, ";
                            break;
                        case 0x6e:
                            mnemo = "BIT 5, ";
                            break;
                        case 0x76:
                            mnemo = "BIT 6, ";
                            break;
                        case 0x7e:
                            mnemo = "BIT 7, ";
                            break;
                        case 0x86:
                            mnemo = "RES 0, ";
                            break;
                        case 0x8e:
                            mnemo = "RES 1, ";
                            break;
                        case 0x96:
                            mnemo = "RES 2, ";
                            break;
                        case 0x9e:
                            mnemo = "RES 3, ";
                            break;
                        case 0xa6:
                            mnemo = "RES 4, ";
                            break;
                        case 0xae:
                            mnemo = "RES 5, ";
                            break;
                        case 0xb6:
                            mnemo = "RES 6, ";
                            break;
                        case 0xbe:
                            mnemo = "RES 7, ";
                            break;
                        case 0xc6:
                            mnemo = "SET 0, ";
                            break;
                        case 0xce:
                            mnemo = "SET 1, ";
                            break;
                        case 0xd6:
                            mnemo = "SET 2, ";
                            break;
                        case 0xde:
                            mnemo = "SET 3, ";
                            break;
                        case 0xe6:
                            mnemo = "SET 4, ";
                            break;
                        case 0xee:
                            mnemo = "SET 5, ";
                            break;
                        case 0xf6:
                            mnemo = "SET 6, ";
                            break;
                        case 0xfe:
                            mnemo = "SET 7, ";
                            break;
                    }
                    break;

                case 0xe1:
                    return "POP IY";
                case 0xe3:
                    return "EX (SP), IY";
                case 0xe5:
                    return "PUSH IY";
                case 0xe9:
                    return "JP (IY)";

                case 0xf9:
                    return "LD SP, IY";
            }

            if (mnemo != null) {
                return String.Format("{0} {1}", mnemo, args).Trim();
            }
            return null;
        }

        /* opcodes sur un unique octet */
        private string DisasmStandardOpcode(byte opcode)
        {
            string mnemo = null;
            string args = String.Empty;
            byte val8;
            ushort val16, addr;
            sbyte dpl;

            switch (opcode) {
                case 0x00:
                    return "NOP";
                case 0x01:
                    mnemo = "LD BC, ";
                    val16 = AddrModeImmediateExtendedValue();
                    args = String.Format("#{0:X4}h", val16);
                    break;
                case 0x02:
                    return "LD (BC), A";
                case 0x03:
                    return "INC BC";
                case 0x04:
                    return "INC B";
                case 0x05:
                    return "DEC B";
                case 0x06:
                    mnemo = "LD B, ";
                    val8 = AddrModeImmediateValue();
                    args = String.Format("#{0:X2}h", val8);
                    break;
                case 0x07:
                    return "RLCA";
                case 0x08:
                    return "EX AF,A'F'";
                case 0x09:
                    return "ADD HL, BC";
                case 0x0a:
                    return "LD A, (BC)";
                case 0x0b:
                    return "DEC BC";
                case 0x0c:
                    return "INC C";
                case 0x0d:
                    return "DEC C";
                case 0x0e:
                    mnemo = "LD C, ";
                    val8 = AddrModeImmediateValue();
                    args = String.Format("#{0:X2}h", val8);
                    break;
                case 0x0f:
                    return "RRCA";

                case 0x10:
                    mnemo = "DJNZ B, ";
                    dpl = AddrModeIndexDisplacement();
                    addr = (ushort)(this.regPC + dpl);
                    args = String.Format(
                            "{0:+000;-000} \t (-> {1:X4}h)",
                            dpl, addr);
                    break;
                case 0x11:
                    mnemo = "LD DE, ";
                    val16 = AddrModeImmediateExtendedValue();
                    args = String.Format("#{0:X4}h", val16);
                    break;
                case 0x12:
                    return "LD (DE), A";
                case 0x13:
                    return "INC DE";
                case 0x14:
                    return "INC D";
                case 0x15:
                    return "DEC D";
                case 0x16:
                    mnemo = "LD D, ";
                    val8 = AddrModeImmediateValue();
                    args = String.Format("#{0:X2}h", val8);
                    break;
                case 0x17:
                    return "RLA";
                case 0x18:
                    mnemo = "JR ";
                    dpl = AddrModeIndexDisplacement();
                    addr = (ushort)(this.regPC + dpl);
                    args = String.Format(
                            "{0:+000;-000} \t (-> {1:X4}h)",
                            dpl, addr);
                    break;
                case 0x19:
                    return "ADD HL, DE";
                case 0x1a:
                    return "LD A, (DE)";
                case 0x1b:
                    return "DEC DE";
                case 0x1c:
                    return "INC E";
                case 0x1d:
                    return "DEC E";
                case 0x1e:
                    mnemo = "LD E, ";
                    val8 = AddrModeImmediateValue();
                    args = String.Format("#{0:X2}h", val8);
                    break;
                case 0x1f:
                    return "RRA";

                case 0x20:
                    mnemo = "JR NZ, ";
                    dpl = AddrModeIndexDisplacement();
                    addr = (ushort)(this.regPC + dpl);
                    args = String.Format(
                            "{0:+000;-000} \t (-> {1:X4}h)",
                            dpl, addr);
                    break;
                case 0x21:
                    mnemo = "LD HL, ";
                    val16 = AddrModeImmediateExtendedValue();
                    args = String.Format("#{0:X4}h", val16);
                    break;
                case 0x22:
                    mnemo = "LD ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("(#{0:X4}h), HL", addr);
                    break;
                case 0x23:
                    return "INC HL";
                case 0x24:
                    return "INC H";
                case 0x25:
                    return "DEC H";
                case 0x26:
                    mnemo = "LD H, ";
                    val8 = AddrModeImmediateValue();
                    args = String.Format("#{0:X2}h", val8);
                    break;
                case 0x27:
                    return "DAA";
                case 0x28:
                    mnemo = "JR Z, ";
                    dpl = AddrModeIndexDisplacement();
                    addr = (ushort)(this.regPC + dpl);
                    args = String.Format(
                            "{0:+000;-000} \t (-> {1:X4}h)",
                            dpl, addr);
                    break;
                case 0x29:
                    return "ADD HL, HL";
                case 0x2a:
                    mnemo = "LD HL, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("({0:X4}h)", addr);
                    break;
                case 0x2b:
                    return "DEC HL";
                case 0x2c:
                    return "INC L";
                case 0x2d:
                    return "DEC L";
                case 0x2e:
                    mnemo = "LD L, ";
                    val8 = AddrModeImmediateValue();
                    args = String.Format("#{0:X2}h", val8);
                    break;
                case 0x2f:
                    return "CPL A";

                case 0x30:
                    mnemo = "JR NC, ";
                    dpl = AddrModeIndexDisplacement();
                    addr = (ushort)(this.regPC + dpl);
                    args = String.Format(
                            "{0:+000;-000} \t (-> {1:X4}h)",
                            dpl, addr);
                    break;
                case 0x31:
                    mnemo = "LD HL, ";
                    val16 = AddrModeImmediateExtendedValue();
                    args = String.Format("#{0:X4}h", val16);
                    break;
                case 0x32:
                    mnemo = "LD ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("({0:X4}h), A", addr);
                    break;
                case 0x33:
                    return "INC SP";
                case 0x34:
                    return "INC (HL)";
                case 0x35:
                    return "DEC (HL)";
                case 0x36:
                    mnemo = "LD (HL), ";
                    val8 = AddrModeImmediateValue();
                    args = String.Format("#{0:X2}h", val8);
                    break;
                case 0x37:
                    return "SCF";
                case 0x38:
                    mnemo = "JR C, ";
                    dpl = AddrModeIndexDisplacement();
                    addr = (ushort)(this.regPC + dpl);
                    args = String.Format(
                            "{0:+000;-000} \t (-> {1:X4}h)",
                            dpl, addr);
                    break;
                case 0x39:
                    return "ADD HL, SP";
                case 0x3a:
                    mnemo = "LD A, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("({0:X4}h)", addr);
                    break;
                case 0x3b:
                    return "DEC SP";
                case 0x3c:
                    return "INC A";
                case 0x3d:
                    return "DEC A";
                case 0x3e:
                    mnemo = "LD A, ";
                    val8 = AddrModeImmediateValue();
                    args = String.Format("#{0:X2}h", val8);
                    break;
                case 0x3f:
                    return "CCF";

                case 0x40:
                    return "LD B, B";
                case 0x41:
                    return "LD B, C";
                case 0x42:
                    return "LD B, D";
                case 0x43:
                    return "LD B, E";
                case 0x44:
                    return "LD B, H";
                case 0x45:
                    return "LD B, L";
                case 0x46:
                    return "LD B, (HL)";
                case 0x47:
                    return "LD B, A";
                case 0x48:
                    return "LD C, B";
                case 0x49:
                    return "LD C, C";
                case 0x4a:
                    return "LD C, D";
                case 0x4b:
                    return "LD C, E";
                case 0x4c:
                    return "LD C, H";
                case 0x4d:
                    return "LD C, L";
                case 0x4e:
                    return "LD C, (HL)";
                case 0x4f:
                    return "LD C, A";

                case 0x50:
                    return "LD D, B";
                case 0x51:
                    return "LD D, C";
                case 0x52:
                    return "LD D, D";
                case 0x53:
                    return "LD D, E";
                case 0x54:
                    return "LD D, H";
                case 0x55:
                    return "LD D, L";
                case 0x56:
                    return "LD D, (HL)";
                case 0x57:
                    return "LD D, A";
                case 0x58:
                    return "LD E, B";
                case 0x59:
                    return "LD E, C";
                case 0x5a:
                    return "LD E, D";
                case 0x5b:
                    return "LD E, E";
                case 0x5c:
                    return "LD E, H";
                case 0x5d:
                    return "LD E, L";
                case 0x5e:
                    return "LD E, (HL)";
                case 0x5f:
                    return "LD E, A";

                case 0x60:
                    return "LD H, B";
                case 0x61:
                    return "LD H, C";
                case 0x62:
                    return "LD H, D";
                case 0x63:
                    return "LD H, E";
                case 0x64:
                    return "LD H, H";
                case 0x65:
                    return "LD H, L";
                case 0x66:
                    return "LD H, (HL)";
                case 0x67:
                    return "LD H, A";
                case 0x68:
                    return "LD L, B";
                case 0x69:
                    return "LD L, C";
                case 0x6a:
                    return "LD L, D";
                case 0x6b:
                    return "LD L, E";
                case 0x6c:
                    return "LD L, H";
                case 0x6d:
                    return "LD L, L";
                case 0x6e:
                    return "LD L, (HL)";
                case 0x6f:
                    return "LD L, A";

                case 0x70:
                    return "LD (HL), B";
                case 0x71:
                    return "LD (HL), C";
                case 0x72:
                    return "LD (HL), D";
                case 0x73:
                    return "LD (HL), E";
                case 0x74:
                    return "LD (HL), H";
                case 0x75:
                    return "LD (HL), L";
                case 0x76:
                    return "HALT";
                case 0x77:
                    return "LD (HL), A";
                case 0x78:
                    return "LD A, B";
                case 0x79:
                    return "LD A, C";
                case 0x7a:
                    return "LD A, D";
                case 0x7b:
                    return "LD A, E";
                case 0x7c:
                    return "LD A, H";
                case 0x7d:
                    return "LD A, L";
                case 0x7e:
                    return "LD A, (HL)";
                case 0x7f:
                    return "LD A, A";

                case 0x80:
                    return "ADD A, B";
                case 0x81:
                    return "ADD A, C";
                case 0x82:
                    return "ADD A, D";
                case 0x83:
                    return "ADD A, E";
                case 0x84:
                    return "ADD A, H";
                case 0x85:
                    return "ADD A, L";
                case 0x86:
                    return "ADD A, (HL)";
                case 0x87:
                    return "ADD A, A";
                case 0x88:
                    return "ADC A, B";
                case 0x89:
                    return "ADC A, C";
                case 0x8a:
                    return "ADC A, D";
                case 0x8b:
                    return "ADC A, E";
                case 0x8c:
                    return "ADC A, H";
                case 0x8d:
                    return "ADC A, L";
                case 0x8e:
                    return "ADC A, (HL)";
                case 0x8f:
                    return "ADC A, A";

                case 0x90:
                    return "SUB A, B";
                case 0x91:
                    return "SUB A, C";
                case 0x92:
                    return "SUB A, D";
                case 0x93:
                    return "SUB A, E";
                case 0x94:
                    return "SUB A, H";
                case 0x95:
                    return "SUB A, L";
                case 0x96:
                    return "SUB A, (HL)";
                case 0x97:
                    return "SUB A, A";
                case 0x98:
                    return "SBC A, B";
                case 0x99:
                    return "SBC A, C";
                case 0x9a:
                    return "SBC A, D";
                case 0x9b:
                    return "SBC A, E";
                case 0x9c:
                    return "SBC A, H";
                case 0x9d:
                    return "SBC A, L";
                case 0x9e:
                    return "SBC A, (HL)";
                case 0x9f:
                    return "SBC A, A";

                case 0xa0:
                    return "AND A, B";
                case 0xa1:
                    return "AND A, C";
                case 0xa2:
                    return "AND A, D";
                case 0xa3:
                    return "AND A, E";
                case 0xa4:
                    return "AND A, H";
                case 0xa5:
                    return "AND A, L";
                case 0xa6:
                    return "AND A, (HL)";
                case 0xa7:
                    return "AND A, A";
                case 0xa8:
                    return "XOR A, B";
                case 0xa9:
                    return "XOR A, C";
                case 0xaa:
                    return "XOR A, D";
                case 0xab:
                    return "XOR A, E";
                case 0xac:
                    return "XOR A, H";
                case 0xad:
                    return "XOR A, L";
                case 0xae:
                    return "XOR A, (HL)";
                case 0xaf:
                    return "XOR A, A";

                case 0xb0:
                    return "OR A, B";
                case 0xb1:
                    return "OR A, C";
                case 0xb2:
                    return "OR A, D";
                case 0xb3:
                    return "OR A, E";
                case 0xb4:
                    return "OR A, H";
                case 0xb5:
                    return "OR A, L";
                case 0xb6:
                    return "OR A, (HL)";
                case 0xb7:
                    return "OR A, A";
                case 0xb8:
                    return "CP A, B";
                case 0xb9:
                    return "CP A, C";
                case 0xba:
                    return "CP A, D";
                case 0xbb:
                    return "CP A, E";
                case 0xbc:
                    return "CP A, H";
                case 0xbd:
                    return "CP A, L";
                case 0xbe:
                    return "CP A, (HL)";
                case 0xbf:
                    return "CP A, A";

                case 0xc0:
                    return "RET NZ";
                case 0xc1:
                    return "POP BC";
                case 0xc2:
                    mnemo = "JP NZ, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("{0:X4}h", addr);
                    break;
                case 0xc3:
                    mnemo = "JP " ;
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("{0:X4}h", addr);
                    break;
                case 0xc4:
                    mnemo = "CALL NZ, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("{0:X4}h", addr);
                    break;
                case 0xc5:
                    return "PUSH BC";
                case 0xc6:
                    mnemo = "ADD A, ";
                    val8 = AddrModeImmediateValue();
                    args = String.Format("#{0:X2}h", val8);
                    break;
                case 0xc7:
                    return "RST 0000h";
                case 0xc8:
                    return "RET Z";
                case 0xc9:
                    return "RET";
                case 0xca:
                    mnemo = "JP Z, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("{0:X4}h", addr);
                    break;
                /* ~~ Opcodes en CB gérés par une autre méthode ! ~~ */
                case 0xcc:
                    mnemo = "CALL Z, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("{0:X4}h", addr);
                    break;
                case 0xcd:
                    mnemo = "CALL ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("{0:X4}h", addr);
                    break;
                case 0xce:
                    mnemo = "ADC A, ";
                    val8 = AddrModeImmediateValue();
                    args = String.Format("#{0:X2}h", val8);
                    break;
                case 0xcf:
                    return "RST 0008h";

                case 0xd0:
                    return "RET NC";
                case 0xd1:
                    return "POP DE";
                case 0xd2:
                    mnemo = "JP NC, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("{0:X4}h", addr);
                    break;
                case 0xd3:
                    mnemo = "OUT ";
                    addr = AddrModeImmediateValue();
                    args = String.Format("({0:X2}h), A", addr);
                    break;
                case 0xd4:
                    mnemo = "CALL NC, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("{0:X4}h", addr);
                    break;
                case 0xd5:
                    return "PUSH DE";
                case 0xd6:
                    mnemo = "SUB A, ";
                    val8 = AddrModeImmediateValue();
                    args = String.Format("#{0:X2}h", val8);
                    break;
                case 0xd7:
                    return "RST 0010h";
                case 0xd8:
                    return "RET C";
                case 0xd9:
                    return "EXX";
                case 0xda:
                    mnemo = "JP C, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("{0:X4}h", addr);
                    break;
                case 0xdb:
                    mnemo = "IN A, ";
                    addr = AddrModeImmediateValue();
                    args = String.Format("({0:X2}h)", addr);
                    break;
                case 0xdc:
                    mnemo = "CALL C, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("{0:X4}h", addr);
                    break;
                /* ~~ Opcodes en DD gérés par une autre méthode ! ~~ */
                case 0xde:
                    mnemo = "SBC A, ";
                    val8 = AddrModeImmediateValue();
                    args = String.Format("#{0:X2}h", val8);
                    break;
                case 0xdf:
                    return "RST 0018h";

                case 0xe0:
                    return "RET PO";
                case 0xe1:
                    return "POP HL";
                case 0xe2:
                    mnemo = "JP PO, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("{0:X4}h", addr);
                    break;
                case 0xe3:
                    return "EX (SP),HL";
                case 0xe4:
                    mnemo = "CALL PO, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("{0:X4}h", addr);
                    break;
                case 0xe5:
                    return "PUSH HL";
                case 0xe6:
                    mnemo = "AND A, ";
                    val8 = AddrModeImmediateValue();
                    args = String.Format("#{0:X2}h", val8);
                    break;
                case 0xe7:
                    return "RST 0020h";
                case 0xe8:
                    return "RET PE";
                case 0xe9:
                    return "JP (HL)";
                case 0xea:
                    mnemo = "JP PE, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("{0:X4}h", addr);
                    break;
                case 0xeb:
                    return "EX DE,HL";
                case 0xec:
                    mnemo = "CALL PE, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("{0:X4}h", addr);
                    break;
                /* ~~ Opcodes en ED gérés par une autre méthode ! ~~ */
                case 0xee:
                    mnemo = "XOR A, ";
                    val8 = AddrModeImmediateValue();
                    args = String.Format("#{0:X2}h", val8);
                    break;
                case 0xef:
                    return "RST 0028h";

                case 0xf0:
                    return "RET PL";
                case 0xf1:
                    return "POP AF";
                case 0xf2:
                    mnemo = "JP PL, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("{0:X4}h", addr);
                    break;
                case 0xf3:
                    return "DI";
                case 0xf4:
                    mnemo = "CALL PL, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("{0:X4}h", addr);
                    break;
                case 0xf5:
                    return "PUSH AF";
                case 0xf6:
                    mnemo = "OR A, ";
                    val8 = AddrModeImmediateValue();
                    args = String.Format("#{0:X2}h", val8);
                    break;
                case 0xf7:
                    return "RST 0030h";
                case 0xf8:
                    return "RET MI";
                case 0xf9:
                    return "LD SP, HL";
                case 0xfa:
                    mnemo = "JP MI, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("{0:X4}h", addr);
                    break;
                case 0xfb:
                    return "EI";
                case 0xfc:
                    mnemo = "CALL MI, ";
                    addr = AddrModeImmediateExtendedValue();
                    args = String.Format("{0:X4}h", addr);
                    break;
                /* ~~ Opcodes en FD gérés par une autre méthode ! ~~ */
                case 0xfe:
                    mnemo = "CP A, ";
                    val8 = AddrModeImmediateValue();
                    args = String.Format("#{0:X2}h", val8);
                    break;
                case 0xff:
                    return "RST 0038h";
            }

            if (mnemo != null) {
                return String.Format("{0} {1}", mnemo, args).Trim();
            }
            return null;
        }

        /* ======================= MÉTHODES PUBLIQUES ======================= */

        /// <summary>
        /// Désassemble une instruction en mémoire.
        /// </summary>
        /// <param name="memoryAddress">
        /// Adresse où débute l'instruction à désassembler.
        /// </param>
        /// <returns></returns>
        /// <exception cref="AddressUnreadableException">
        /// Si l'une des adresses-mémoire à traiter est impossible à lire.
        /// </exception>
        public String DisassembleInstructionAt(ushort memoryAddress)
        {
            StringBuilder sbResult = new StringBuilder();
            this.regPC = memoryAddress;

            /* écrit d'abord l'adresse traitée */
            sbResult.Append(String.Format("{0:X4} : ", this.regPC));

            /* analyse l'opcode trouvé à cette adresse */
            string instr;
            bool opcodeDouble = false;
            byte opcode = ReadMem(this.regPC);
            this.regPC++;
            switch (opcode) {
                case 0xcb:
                    // opcodes de rotations, décalages
                    // et autres manipulations bit-à-bit
                    opcode = ReadMem(this.regPC);
                    this.regPC++;
                    instr = DisasmCBopcode(opcode);
                    opcodeDouble = true;
                    break;
                case 0xdd:
                    // opcodes sur le registre d'index IX
                    opcode = ReadMem(this.regPC);
                    this.regPC++;
                    instr = DisasmDDopcode(opcode);
                    opcodeDouble = true;
                    break;
                case 0xed:
                    // opcodes spéciaux
                    opcode = ReadMem(this.regPC);
                    this.regPC++;
                    instr = DisasmEDopcode(opcode);
                    opcodeDouble = true;
                    break;
                case 0xfd:
                    // opcodes sur le registre d'index IY
                    opcode = ReadMem(this.regPC);
                    this.regPC++;
                    instr = DisasmFDopcode(opcode);
                    opcodeDouble = true;
                    break;
                default:
                    // opcodes sur un seul octet (par défaut)
                    instr = DisasmStandardOpcode(opcode);
                    break;
            }

            /* opcode invalide ! */
            if (instr == null) {
                switch (this.uoPolicy) {
                    case UnknownOpcodePolicy.DoNop:
                        instr = "?!?";
                        break;
                    case UnknownOpcodePolicy.ThrowException:
                    default:
                        throw new UnknownOpcodeException(
                                this.regPC, opcode,
                                String.Format(ERR_UNKNOWN_OPCODE,
                                              this.regPC, opcode));
                }
            }

            /* liste les octets ainsi traités */
            int nbOct = this.regPC - memoryAddress;
            for (int n = 0; n < nbOct; n++) {
                ushort ad = (ushort)(memoryAddress + n);
                byte b = ReadMem(ad);
                sbResult.Append(String.Format("{0:X2} ", b));
                if ((n == 0) && !opcodeDouble) {
                    sbResult.Append("   ");
                }
            }
            /* aligne le résultat sur 24 colonnes */
            while (sbResult.Length < 24) sbResult.Append(" ");
            sbResult.Append(": ");

            /* enfin, liste l'instruction désassemblée */
            sbResult.Append(instr);

            /* terminé */
            sbResult.Append(" \r\n");
            return sbResult.ToString();
        }

        /// <summary>
        /// Désassemble un nombre donné d'instructions en mémoire.
        /// </summary>
        /// <param name="fromAddress">
        /// Adresse mémoire de la première instruction à désassembler.
        /// </param>
        /// <param name="nbInstr">
        /// Nombre d'instructions consécutives à desassembler.
        /// </param>
        /// <returns>
        /// Chaîne de caractère contenant le désassemblage des instructions
        /// rencontrées à partir de <code>fromAddress</code>.
        /// </returns>
        /// <exception cref="AddressUnreadableException">
        /// Si l'une des adresses-mémoire à traiter est impossible à lire.
        /// </exception>
        public String DisassembleManyInstructionsAt(ushort fromAddress,
                                                    uint nbInstr)
        {
            StringBuilder sbResult = new StringBuilder();
            this.regPC = fromAddress;
            for (uint n = 0; n < nbInstr; n++) {
                string instr = DisassembleInstructionAt(
                        (ushort)(this.regPC));
                sbResult.Append(instr);
            }
            return sbResult.ToString();
        }

        /// <summary>
        /// Désassemble le contenu d'une plage d'adresses en mémoire.
        /// </summary>
        /// <param name="fromAddress">
        /// Adresse mémoire de la première instruction à désassembler.
        /// </param>
        /// <param name="toAddress">
        /// Dernière adresse mémoire à desassembler.
        /// </param>
        /// <returns>
        /// Chaîne de caractère contenant le désassemblage des adresses
        /// de la plage mémoire indiquée.
        /// <br/>
        /// Notez que le désassemblage peut aller légèrement au-delà de
        /// <code>toAddress</code> si une instruction s'étend sur cette
        /// adresse de fin.
        /// </returns>
        /// <exception cref="AddressUnreadableException">
        /// Si l'une des adresses-mémoire à traiter est impossible à lire.
        /// </exception>
        public String DisassembleMemory(ushort fromAddress,
                                        ushort toAddress)
        {
            StringBuilder sbResult = new StringBuilder();
            this.regPC = fromAddress;
            while (this.regPC <= toAddress) {
                string instr = DisassembleInstructionAt(
                        (ushort)(this.regPC));
                sbResult.Append(instr);
            }
            return sbResult.ToString();
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
        /// Politique de prise en charge des opcodes invalides
        /// au désassemblage.
        /// </summary>
        public UnknownOpcodePolicy InvalidOpcodePolicy
        {
            get { return this.uoPolicy; }
            set { this.uoPolicy = value; }
        }

    }
}

