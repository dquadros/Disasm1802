# Disasm1802
CDP1802 Disassembler

This is a very crude disassembler for the RCA CDP1892 microprocessor.

It accepts as input an Intel Hex file (checksums are ignored and may be omitted). An optional .def file can specify what areas should be treated as code or data (by default all bytes are treated as code) and label for addresses. More details (for now) are in the code.

The main objective is to disassembly binary code from old examples. Testing so far was done with the Monitor Prom for Netronics Giant Board (as listed in march 1978 issue of Popular Electronics).
