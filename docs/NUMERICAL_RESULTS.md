# Executed numerical results

Generated from the Unity validator CSVs. These are model outputs, not measured plant data.

| Varied factor | Tested range | Refined methanol min..max (kg/h) | Interpretation |
|---|---|---|---|
| temperature | 200..320 degC | 69.974..261.122 | Heuristic peak near 240 C; decline away from optimum. |
| pressure | 40..100 bar | 219.366..274.646 | Pressure factor increases calculated conversion. |
| ratio | 1.5..4.5 mol/mol | 142.008..254.218 | Penalty plus reactant availability; not general catalyst kinetics. |
| feed | 40..100 percent | 100.915..252.288 | Throughput changes; per-pass conversion stays fixed. |
| recycle | 0..96 percent | 127.298..474.471 | Internal reuse raises overall utilization. |

Recycle benchmark: 240 cases. Maximum absolute methanol difference from the closed-form oracle: 0.000197589086 kg/h. Maximum external closure error: 9.98917148e-08%.

The 200..320 C batch sensitivity range includes stress points outside the 180..300 C UI sweep; the two grids are not claimed identical. The fixed nominal input JSON supplies all held-constant parameters. Numerical tolerances and external physical-validation limits are documented in SCIENTIFIC_VALIDATION.md.
