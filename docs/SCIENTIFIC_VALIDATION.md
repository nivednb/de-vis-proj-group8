# Scientific basis and validation

Release branch: `final-submission`. This document distinguishes numerical verification from empirical validation. The application is an educational steady-state process simulator with a 3D visualization; there is no live plant-data connection.

## Research question and contribution

Can a shared, lightweight process model make the relationships between operating inputs, material streams, recycle and product recovery inspectable in an interactive whole-plant teaching application? The contribution is the coupling of deterministic calculations, species-aware visualization, controllable inputs, and reproducible one-factor-at-a-time (OFAT) experiments. No learning-effectiveness study was conducted, so improved student learning is a hypothesis, not a measured result.

## Electrolysis

Water electrolysis follows `2 H2O -> 2 H2 + O2` [S1]. Using the same mass basis as the synthesis model, hydrogen production is the minimum of electrical capacity and water mass times `2.01588/18.01528`. Oxygen is hydrogen times `(18.01528-2.01588)/2.01588`; unused water remains an unreacted feed remainder. This is a stoichiometric capacity calculation, not an electrical-efficiency model. The previous nonzero water-response intercept incorrectly permitted production with no water and was removed.

## Independent synthesis-loop benchmark

Let fresh molar feeds be F_C (CO2) and F_H (H2), per-pass conversion x, recycled fraction r, and reaction extent e in kmol/h. Products are perfectly removed before recycle, both gas species have the same split, and there are no inerts or side reactions.

At steady state, the reactor feeds are:

`C_in = (F_C - r e)/(1-r)`

`H_in = (F_H - 3 r e)/(1-r)`

Substitution into `e = min(x C_in, H_in/3)` independently gives:

`e = min(x F_C/[1-r(1-x)], F_H/3)`.

The production code uses fixed-point iteration; the validator uses this closed-form expression. Methanol and water are `32.04186 e` and `18.01528 e` kg/h. External purge is `F_C-e` and `F_H-3e` kmol/h. Carbon, hydrogen and total external mass must close. Internal recycle must not be counted as an external input/output.

For 1,152 kg/h CO2, 158 kg/h H2, x=0.20 and r=0.95, the CO2 conversion is approximately 83.3333% and methanol is approximately 698.94 kg/h before downstream recovery. This is an analytical test case, NOT a measured plant case.

`SubmissionValidation.Run` compares 240 cases across zero reactants, zero conversion, zero recycle, H2-limited feed and recycle up to 0.999. It checks production against the analytical solution, atom balances, nonnegative purge, solver convergence and external closure below 0.001%. The first expanded run failed external closure; its stopping criterion was scaled to the large internal recycle rather than fresh feed. The corrected solver uses fresh-feed scaling, relative tolerance 1e-10 and a 32,768-iteration cap. `r=1` is singular and is internally bounded to 0.999; the UI's 100% endpoint is therefore an approximation and must not be described as a sealed no-purge steady state.

## Empirical context and limits

[S2] reports experiments at 240-280 C, 40-80 bar and H2/CO2=3.0-3.4, supporting the use of these variables as educational controls. It does NOT validate our uncalibrated response formula. Its reported improvement with increased hydrogen ratio also cautions against treating our symmetric penalty around ratio 3 as universal kinetics.

[S3], section 3.2/Table 2, reports 17.8% CO2 conversion for its CZ-2 catalyst at 240 C and 30 bar, H2/CO2=3. These are useful external context, but the catalyst, space-velocity basis, CO side-product and selectivity differ from this application's assumptions. We cannot honestly calculate a matched-case validation error from those data. We do not fit the application to that single point or claim that numerical agreement would establish physical validity.

A rigorous quantitative validation remains a separate research task: obtain the original experimental dataset or a reproducible DWSIM/Aspen case, align catalyst, feed, residence-time basis and separation boundaries, account for CO/selectivity, predefine tolerances, and report residuals over multiple operating points. No such calibrated dataset was provided or generated here.

## Sensitivity methodology

`docs/evidence/nominal-inputs.json` records the complete baseline. The actual Unity `Simulate` method produces 13 points each for temperature, pressure, ratio, feed and recycle (65 points), varying only the named field. `sensitivity.csv` stores units, single-pass conversion, refined methanol rate and recovery index. The validator also proves the input baseline is not mutated.

Temperature peaks near the model's specified 240 C optimum; pressure raises the conversion correlation; ratio affects both a heuristic penalty and feed availability; feed changes throughput but not the single-pass correlation; recycle changes overall utilization, not per-pass conversion. These are consequences of the implemented assumptions, not experimentally discovered laws. The graphs are deterministic model experiments, not measured plant data or statistical causal inference.

See [Executed numerical results](NUMERICAL_RESULTS.md) for the measured numerical residuals and sensitivity ranges.

## Model boundaries

- The dashboard's legacy `overallEfficiencyPercent` is refined methanol divided by stoichiometric potential, not energy efficiency. Electricity, compressor work and heat duties are not calculated.
- Capture, condenser recovery, purity and distillation energy are educational correlations. Their coefficients and design capacities are project assumptions, not values validated by the cited papers.
- The 1,250 kg/h methanol design value is a cap, not a guaranteed nominal result.
- The plant-load schedule gives 95% ramp at timeline=100; this is preserved existing behavior.
- Storage evolves on an accelerated clock; process streams otherwise represent steady states. Display smoothing is not a dynamic process model.
- Reactor color and packet animations communicate state; they are not molecular trajectories, CFD, literal catalyst color or certified safety instrumentation.
- No life-cycle emissions, economics, equipment sizing, thermal safety or user-study claims are established.

## Sources (accessed 2026-09-22)

[S1] US Department of Energy, *Hydrogen Production: Electrolysis*, reaction description. https://www.energy.gov/cmei/fuels/hydrogen-production-electrolysis

[S2] Che Yifei, Li Tao, Zhang Haitao (2020), *Intrinsic Kinetics of Hydrogenation of CO2 towards Methanol on a Cu/ZnO/Al2O3 Modified Catalyst*, 46(3), 326-333. DOI: 10.14135/j.cnki.1006-3080.20190227001. Publisher abstract consulted: https://journal.ecust.edu.cn/en/article/doi/10.14135/j.cnki.1006-3080.20190227001

[S3] Lei, Zheng and Liu (2019), *Cylindrical shaped ZnO combined Cu catalysts for the hydrogenation of CO2 to methanol*, RSC Advances 9, 13696-13704. DOI: 10.1039/C9RA00658C. Publisher indexed section 3.2 consulted: https://pubs.rsc.org/en/content/articlehtml/2019/ra/c9ra00658c

[S4] European Commission JRC (2016), *Techno-economic and environmental evaluation of CO2 utilisation for fuel production. Synthesis of methanol and formic acid*, JRC99380. Used only to contextualize system boundaries; no cost/emission results are transferred into this model. https://publications.jrc.ec.europa.eu/repository/handle/JRC99380

## Frozen-source evidence

The final clean-clone suite executed 1,586 assertions on source `9e3ea9c192d506e33e4227586efdd5cd5da994bd`. Editor CSV closure was 1.13699e-09%; player CSV closure was 4.25878532e-09%. These are numerical residuals for their respective operating points. Re-summing rounded exported stream values can produce a larger residual (0.0002 kg/h in the 25%-recycle regression), so the CSV's rounded zero must not be described as exact physical closure. Full logs/results and empirical-validation limitations are separated in SUBMISSION_STATUS.md.
