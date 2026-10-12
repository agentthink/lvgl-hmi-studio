# Normalized LCEDA-Pro netlist source

`normalized-netlist.csv` expands all compact matrix expressions into one row per page/component/net connection. It is suitable as a deterministic handoff to LCEDA-Pro manual capture or a future netlist importer.

- Source: `lceda-page-net-matrix.csv`
- LCD source: `lcd-50pin-pinout.csv`
- T113 source: `t113-lcd-net-assignment.csv`
- Generator: `generate_netlist.py`

This is a connection source table, not a native LCEDA-Pro project file. Native LCEDA capture and ERC must still be performed in the LCEDA-Pro GUI.
