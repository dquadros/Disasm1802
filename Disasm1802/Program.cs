/*
 * DISASM1802 - CDP1802 Disassembler
 * 
 * Disassembles code for the RCA CDP1892 microprocessor
 * 
 * Usage: Disasm1802 name
 * 
 *        Binary code will be read from name.hex (Intel hex format)
 *        Optional information can be provided in nome.def
 *        Output will sent to the console
 * 
 * (C) 2022, Daniel Quadros
 * 
 * Permission is hereby granted, free of charge, to any person obtaining 
 * a copy of this software and associated documentation files (the "Software"),
 * to deal in the Software without restriction, including without limitation 
 * the rights to use, copy, modify, merge, publish, distribute, sublicense, 
 * and/or sell copies of the Software, and to permit persons to whom the 
 * Software is furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included 
 * in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS 
 * OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
 * THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR 
 * OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, 
 * ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR 
 * OTHER DEALINGS IN THE SOFTWARE. 
 * 
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Globalization;

namespace Disasm1802
{
    // Definition of a memory area
    class MemArea: IComparable<MemArea>
    {
        public int start, end;
        public enum MType { CODE, DATA } ;
        public MType type;

        public MemArea (MType type, int start, int end) {
            this.start = start;
            this.end = end;
            this.type = type;
        }

        public int CompareTo(MemArea m)
        {
            return start.CompareTo(m.start);
        }
    }

    // The Symbol table
    class SymbolTable
    {
        // The table is stored in a dictionary
        private Dictionary<int, string> symtable;

        // Constructor
        public SymbolTable()
        {
            symtable = new Dictionary<int, string>();
        }

        // Check if there is a name for an address
        public bool HasName(int addr) 
        {
            return symtable.ContainsKey(addr);
        }

        // Add a symbol
        public void Add(string name, int addr) 
        {
            symtable.Add(addr, name);
        }

        // Get a name for an address
        public string GetName(int addr)
        {
            if (HasName(addr)) 
            {
                return symtable[addr];
            }
            else
            {
                return "L"+addr.ToString("X4");
            }
        }

        public int Count
        {
            get { return symtable.Count; }
        }

    }
        
    // Instruction info for disassembly
    public class Instruction
    {
        public enum Operand { NONE, REG0, REG1, DEV, IMMED8, ADDR8, ADDR16 };
        public byte code;
        public Operand operand;
        public string name;

        public Instruction(string name, byte code, Operand operand)
        {
            this.name = name;
            this.code = code;
            this.operand = operand;
        }

    }

    // Instruction Set
    public class InstructionSet
    {
        private Instruction[] codes;
        private List<Instruction> instr;

        public InstructionSet()
        {
            // Load instructions
            instr = new List<Instruction>();
               // Control
            instr.Add(new Instruction("IDL",  0x00, Instruction.Operand.NONE));
            instr.Add(new Instruction("NOP",  0xC4, Instruction.Operand.NONE));
            instr.Add(new Instruction("SEP",  0xD0, Instruction.Operand.REG0));
            instr.Add(new Instruction("SEX",  0xE0, Instruction.Operand.REG0));
            instr.Add(new Instruction("SEQ",  0x7B, Instruction.Operand.NONE));
            instr.Add(new Instruction("REQ",  0x7A, Instruction.Operand.NONE));
            instr.Add(new Instruction("SAV",  0x78, Instruction.Operand.NONE));
            instr.Add(new Instruction("MARK", 0x79, Instruction.Operand.NONE));
            instr.Add(new Instruction("RET",  0x70, Instruction.Operand.NONE));
            instr.Add(new Instruction("DIS",  0x71, Instruction.Operand.NONE));
              // Memory Reference
            instr.Add(new Instruction("LDN",  0x00, Instruction.Operand.REG1));
            instr.Add(new Instruction("LDA",  0x40, Instruction.Operand.REG0));
            instr.Add(new Instruction("LDX",  0xF0, Instruction.Operand.NONE));
            instr.Add(new Instruction("LDXA", 0x72, Instruction.Operand.NONE));
            instr.Add(new Instruction("LDI",  0xF8, Instruction.Operand.IMMED8));
            instr.Add(new Instruction("STR",  0x50, Instruction.Operand.REG0));
            instr.Add(new Instruction("STXD", 0x73, Instruction.Operand.NONE));
              // register Operations
            instr.Add(new Instruction("INC",  0x10, Instruction.Operand.REG0));
            instr.Add(new Instruction("DEC",  0x20, Instruction.Operand.REG0));
            instr.Add(new Instruction("IRX",  0x60, Instruction.Operand.NONE));
            instr.Add(new Instruction("GLO",  0x80, Instruction.Operand.REG0));
            instr.Add(new Instruction("PLO",  0xA0, Instruction.Operand.REG0));
            instr.Add(new Instruction("GHI",  0x90, Instruction.Operand.REG0));
            instr.Add(new Instruction("PHI",  0xB0, Instruction.Operand.REG0));
              // logic operations
            instr.Add(new Instruction("OR",   0xF1, Instruction.Operand.NONE));
            instr.Add(new Instruction("ORI",  0xF9, Instruction.Operand.IMMED8));
            instr.Add(new Instruction("XOR",  0xF3, Instruction.Operand.NONE));
            instr.Add(new Instruction("XRI",  0xFB, Instruction.Operand.IMMED8));
            instr.Add(new Instruction("AND",  0xF2, Instruction.Operand.NONE));
            instr.Add(new Instruction("ANI",  0xFA, Instruction.Operand.IMMED8));
            instr.Add(new Instruction("SHR",  0xF6, Instruction.Operand.NONE));
            instr.Add(new Instruction("SHRC", 0x76, Instruction.Operand.NONE));
            instr.Add(new Instruction("SHL",  0xFE, Instruction.Operand.NONE));
            instr.Add(new Instruction("SHLC", 0x7E, Instruction.Operand.NONE));
              // arithmetic operations
            instr.Add(new Instruction("ADD",  0xF4, Instruction.Operand.NONE));
            instr.Add(new Instruction("ADI",  0xFC, Instruction.Operand.IMMED8));
            instr.Add(new Instruction("ADC",  0x74, Instruction.Operand.NONE));
            instr.Add(new Instruction("ADCI", 0x7C, Instruction.Operand.IMMED8));
            instr.Add(new Instruction("SD",   0xF5, Instruction.Operand.NONE));
            instr.Add(new Instruction("SDI",  0xFD, Instruction.Operand.IMMED8));
            instr.Add(new Instruction("SDB",  0x75, Instruction.Operand.NONE));
            instr.Add(new Instruction("SDBI", 0x7D, Instruction.Operand.IMMED8));
            instr.Add(new Instruction("SM",   0xF7, Instruction.Operand.NONE));
            instr.Add(new Instruction("SMI",  0xFF, Instruction.Operand.IMMED8));
            instr.Add(new Instruction("SMB",  0x77, Instruction.Operand.NONE));
            instr.Add(new Instruction("SMBI", 0x7F, Instruction.Operand.IMMED8));
              // short branch
            instr.Add(new Instruction("BR",   0x30, Instruction.Operand.ADDR8));
            instr.Add(new Instruction("BZ",   0x32, Instruction.Operand.ADDR8));
            instr.Add(new Instruction("BNZ",  0x3A, Instruction.Operand.ADDR8));
            instr.Add(new Instruction("BDF",  0x33, Instruction.Operand.ADDR8));
            instr.Add(new Instruction("BNF",  0x3B, Instruction.Operand.ADDR8));
            instr.Add(new Instruction("BQ",   0x31, Instruction.Operand.ADDR8));
            instr.Add(new Instruction("BNQ",  0x39, Instruction.Operand.ADDR8));
            instr.Add(new Instruction("B1",   0x34, Instruction.Operand.ADDR8));
            instr.Add(new Instruction("BN1",  0x3C, Instruction.Operand.ADDR8));
            instr.Add(new Instruction("B2",   0x35, Instruction.Operand.ADDR8));
            instr.Add(new Instruction("BN2",  0x3D, Instruction.Operand.ADDR8));
            instr.Add(new Instruction("B3",   0x36, Instruction.Operand.ADDR8));
            instr.Add(new Instruction("BN3",  0x3E, Instruction.Operand.ADDR8));
            instr.Add(new Instruction("B4",   0x37, Instruction.Operand.ADDR8));
            instr.Add(new Instruction("BN4",  0x3F, Instruction.Operand.ADDR8));
              // long branch
            instr.Add(new Instruction("LBR",  0xC0, Instruction.Operand.ADDR16));
            instr.Add(new Instruction("LBZ",  0xC2, Instruction.Operand.ADDR16));
            instr.Add(new Instruction("LBNZ", 0xCA, Instruction.Operand.ADDR16));
            instr.Add(new Instruction("LBDF", 0xC3, Instruction.Operand.ADDR16));
            instr.Add(new Instruction("LBNF", 0xCB, Instruction.Operand.ADDR16));
            instr.Add(new Instruction("LBQ",  0xC1, Instruction.Operand.ADDR16));
            instr.Add(new Instruction("LBNQ", 0xC9, Instruction.Operand.ADDR16));
              // skip
            instr.Add(new Instruction("SKP",  0x38, Instruction.Operand.NONE));
            instr.Add(new Instruction("LSKP", 0xC8, Instruction.Operand.NONE));
            instr.Add(new Instruction("LSZ",  0xCE, Instruction.Operand.NONE));
            instr.Add(new Instruction("LSNZ", 0xC6, Instruction.Operand.NONE));
            instr.Add(new Instruction("LSDF", 0xCF, Instruction.Operand.NONE));
            instr.Add(new Instruction("LSNF", 0xC7, Instruction.Operand.NONE));
            instr.Add(new Instruction("LSQ",  0xCD, Instruction.Operand.NONE));
            instr.Add(new Instruction("LSNQ", 0xC5, Instruction.Operand.NONE));
            instr.Add(new Instruction("LSNE", 0xCC, Instruction.Operand.NONE));
              // input-output
            instr.Add(new Instruction("OUT",  0x60, Instruction.Operand.DEV));
            instr.Add(new Instruction("IN",   0x68, Instruction.Operand.DEV));

            // Map codes to instructions
            codes = new Instruction[256];
            foreach (Instruction i in instr)
            {
                if (i.operand == Instruction.Operand.REG0)
                {
                    for (int j = 0; j < 16; j++)
                    {
                        codes[i.code + j] = i;
                    }
                }
                else if (i.operand == Instruction.Operand.REG1)
                {
                    for (int j = 1; j < 16; j++)
                    {
                        codes[i.code + j] = i;
                    }
                }
                else if (i.operand == Instruction.Operand.DEV)
                {
                    for (int j = 1; j < 8; j++)
                    {
                        codes[i.code + j] = i;
                    }
                }
                else
                {
                    codes[i.code] = i;
                }
            }
        }

        // Find instruction from code
        public Instruction GetInst(byte code)
        {
            if (codes[code] == null)
            {
                return new Instruction("???", code, Instruction.Operand.NONE);
            }
            else
            {
                return codes[code];
            }
        }

        // For debug
        public void Dump()
        {
            for (int lo = 0; lo < 16; lo++)
            {
                for (int hi = 0; hi < 16; hi++)
                {
                    Instruction i = codes[hi * 16 + lo];
                    string mne = (i == null) ? "???" : i.name;
                    Console.Out.Write(mne.PadRight(5));
                }
                Console.Out.WriteLine();
            }
        }

    }


    // Main Class
    class Program
    {
        private static string HexFile;
        private static string DefFile;
        private static byte [] memory;
        private static int start, end;
        private static List<MemArea> areas;
        private static SymbolTable st;
        private static InstructionSet iset;

        static void Main(string[] args)
        {
            try
            {
                Console.Out.WriteLine("DISASM1802 v"+
                    Assembly.GetExecutingAssembly().GetName().Version.Major.ToString() + "." +
                    Assembly.GetExecutingAssembly().GetName().Version.Minor.ToString("D2"));
                Console.Out.WriteLine("(C) 2022, Daniel Quadros https://dqsoft.blogspot.com");
                Console.Out.WriteLine();
                if (Init(args))
                {
                    iset = new InstructionSet();
                    Disasm(false);      // get labels
                    Disasm(true);       // generate output
                }
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine("FATAL ERROR: " + ex.Message);
            }
            Console.In.Read();
        }

        // Initialization
        // Returns false in case of a serious error
        static bool Init(string[] args)
        {
            // Check parameters
            if (!ParseParam(args))
            {
                Console.Out.WriteLine("usage: DISASM1802 name");
                return false;
            }

            // Load hex file
            if (!LoadHex(HexFile))
            {
                Console.Out.WriteLine("Error loading "+HexFile);
                return false;
            }

            // Define area and symbols
            areas = new List<MemArea>();
            st = new SymbolTable();
            if (File.Exists(DefFile))
            {
                // Load from file
                if (!LoadDef(DefFile))
                {
                    Console.Out.WriteLine("Error loading "+DefFile);
                    return false;
                }
            }
            else 
            {
                // No definiton file, assume all is code, no symbols
                areas.Add(new MemArea (MemArea.MType.CODE, start, end));
            }

            return true;
        }

        // Parses parameters - returns false if invalid
        // For now accepts only the base name for the input files
        static bool ParseParam(string[] args)
        {
            if (args.Length < 1)
            {
                return false;
            }
            HexFile = args[0];
            if (!Path.HasExtension(HexFile))
            {
                HexFile = HexFile + ".hex";
            }
            DefFile = Path.ChangeExtension(HexFile, ".def");
            return true;
        }

        // Load hex file into memory array
        // Intel Hex format
        // :llaaaattdd...ddcc
        // ll = length, aaaa = address, tt = type, dd = hexadecimal data, cc = checksum
        // return false if error
        static bool LoadHex(string file)
        {
            try
            {
                if (!File.Exists(file))
                {
                    Console.Out.WriteLine("ERROR: " + file + " not found");
                    return false;
                }
                memory = new byte[64 * 1024];
                start = 0xFFFF;
                end = 0;
                using (StreamReader sr = new StreamReader(file))
                {
                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine();
                        if ((line != null) && (line.Length > 9) && (line[0] == ':') && (line.Substring(7, 2) == "00"))
                        {
                            int len = int.Parse(line.Substring(1, 2), NumberStyles.HexNumber);
                            int addr = int.Parse(line.Substring(3, 4), NumberStyles.HexNumber);
                            if (addr < start)
                            {
                                start = addr;
                            }
                            for (int i = 0; i < len; i++)
                            {
                                memory[addr] = byte.Parse(line.Substring(9 + 2 * i, 2), NumberStyles.HexNumber);
                                addr++;
                            }
                            if (addr > end)
                            {
                                end = addr;
                            }
                        }
                    }
                }
                Console.Out.WriteLine(file + " loaded: start=0x" + start.ToString("X4") + ", end=0x" + end.ToString("X4"));
                return true;
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine("FATAL ERROR: " + ex.Message);
                return false;
            }
        }

        // Load area and symbol definitions
        // Definition file lines format:
        //   CODE start end
        //   DATA start end
        //   addr name
        static bool LoadDef(string file)
        {
            try
            {
                using (StreamReader sr = new StreamReader(file))
                {
                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine();
                        if (line != null)
                        {
                            string [] fields = line.Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (fields.Length > 2)
                            {
                                if (fields[0] == "CODE")
                                {
                                    areas.Add(new MemArea(MemArea.MType.CODE, int.Parse(fields[1], NumberStyles.HexNumber),
                                        int.Parse(fields[2], NumberStyles.HexNumber)));
                                }
                                else if (fields[0] == "DATA")
                                {
                                    areas.Add(new MemArea(MemArea.MType.DATA, int.Parse(fields[1], NumberStyles.HexNumber),
                                        int.Parse(fields[2], NumberStyles.HexNumber)));
                                }
                            }
                            else if (fields.Length > 1)
                            {
                                st.Add(fields[1], int.Parse(fields[0], NumberStyles.HexNumber));
                            }
                        }
                    }
                }
                areas.Sort();
                Console.Out.WriteLine(file + " loaded: " + areas.Count + " areas, " + st.Count + " symbols");
                return true;
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine("FATAL ERROR: " + ex.Message);
                return false;
            }
        }

        // Main disassembly rotine
        static void Disasm(bool emit)
        {
            int iArea = 0;
            MemArea area = areas[0];

            string data = "";       // buffer for outputing data bytes

            for (int addr = start; addr < end; addr++)
            {
                if (emit && st.HasName(addr))
                {
                    if (data.Length > 0)
                    {
                        Console.Out.WriteLine("    " + data);
                        data = "";
                    }
                    Console.Out.WriteLine(st.GetName(addr) + ":");
                }

                MemArea.MType mtype = MemArea.MType.DATA;   // assume DATA if out of an area
                if (addr >= area.start)
                {
                    int oldArea = iArea;
                    while ((iArea < (areas.Count-1)) && (addr >= area.end))
                    {
                        iArea++;
                        area = areas[iArea];
                    }
                    if ((data.Length > 0) && (oldArea != iArea))
                    {
                        if (emit)
                        {
                            Console.Out.WriteLine("    " + data);
                        }
                        data = "";
                    }
                }

                if ((addr >= area.start) && (addr < area.end))
                {
                    mtype = area.type;
                }

                if (mtype == MemArea.MType.CODE)
                {
                    int reg, dev, operand, target;
                    string name;
                    byte code = memory[addr];
                    Instruction i = iset.GetInst(code);
                    switch (i.operand)
                    {
                        case Instruction.Operand.NONE:
                            if (emit)
                            {
                                Console.Out.WriteLine("    "+i.name);
                            }
                            break;
                        case Instruction.Operand.REG0:
                        case Instruction.Operand.REG1:
                            reg = code & 0x0F;
                            if (emit)
                            {
                                Console.Out.WriteLine("    " + i.name.PadRight(5)+"R"+reg.ToString());
                            }
                            break;
                        case Instruction.Operand.DEV:
                            dev = code & 0x07;
                            if (emit)
                            {
                                Console.Out.WriteLine("    " + i.name.PadRight(5)+dev.ToString());
                            }
                            break;
                        case Instruction.Operand.IMMED8:
                            operand = memory[++addr];
                            if (emit)
                            {
                                Console.Out.WriteLine("    " + i.name.PadRight(5)+"#"+operand.ToString("X2"));
                            }
                            break;
                        case Instruction.Operand.ADDR8:
                            target = (addr & 0xFF00) + memory[++addr];
                            name = st.GetName(target);
                            if (!st.HasName(target))
                            {
                                st.Add(name, target);
                            }
                            if (emit)
                            {
                                Console.Out.WriteLine("    " + i.name.PadRight(5)+name);
                            }
                            break;
                        case Instruction.Operand.ADDR16:
                            target = (memory[++addr] << 16) + memory[++addr];
                            name = st.GetName(target);
                            if (!st.HasName(target))
                            {
                                st.Add(name, target);
                            }
                            if (emit)
                            {
                                Console.Out.WriteLine("    " + i.name.PadRight(5)+name);
                            }
                            break;
                    }
                }
                else if (emit)
                {
                    data += ", #" + memory[addr].ToString("X2");
                    if (data.Length > 60)
                    {
                        Console.Out.WriteLine("    " + data);
                        data = "";
                    }
                }

            }
            if (emit)
            {
                if (data.Length > 0)
                {
                    Console.Out.WriteLine("    " + data);
                    data = "";
                }
                Console.Out.WriteLine("    END");
            }
        }

    }
}
