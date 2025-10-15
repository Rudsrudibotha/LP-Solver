🧮 LP-Solver

LP-Solver is a project I developed to implement a linear programming solver from scratch (or semi-from scratch), demonstrating algorithmic understanding of optimization.
It can read LP problems, run a solving algorithm (Simplex / interior point / dual / etc.), and output optimal (or infeasible/unbounded) solutions.

🧠 Overview

This project is meant as a learning / demonstration tool — not a full industrial solver — showing how to:

Parse and represent linear programming (LP) problems

Implement the core solving algorithm(s)

Handle edge cases (infeasibility, unboundedness)

Report solution status and variable values

The structure is modular so you can plug in alternate solving methods later (e.g. dual simplex, interior point, etc.).

⚙️ Features

Parser/reader for LP problem input (e.g. objective, constraints, bounds)

Internal data structures for storing coefficient matrices, bounds, variables

Implementation of a solving algorithm (for example, Simplex, Revised Simplex, or Interior Point)

Handling of special cases (infeasible, unbounded, degeneracy)

Output of solution: status, objective value, variable assignments

(Optionally) debugging, pivot tracking, logs

🚀 Technologies / Theory Used

Programming language: (Your language: C#, Java, Python, etc.)

Algorithms: Simplex method, possibly Revised Simplex, dual simplex, or Interior Point / barrier method

Linear algebra: matrix and vector operations (row operations, pivoting)

Numerical stability: handling floating point precision, pivot selection heuristics

Data structures: sparse or dense matrices, arrays, lists

📂 Project Structure (Example)

Here’s a possible directory layout:

LP-Solver/
│
├── Core/
│   ├── Model.cs / model.py / Model.java
│   ├── SimplexSolver.cs / Solver.py
│   └── Utilities / LinearAlgebra
│
├── IO/
│   ├── Parser.cs / Parser.py
│   └── LPReader / Writer
│
├── Tests/
│   └── UnitTests / ExampleProblems
│
├── Examples/
│   └── sample_lp1.lp, sample_lp2.lp
│
├── README.md
└── Program.cs / main.py / Main.java
