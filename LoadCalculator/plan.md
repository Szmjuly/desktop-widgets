## Core idea

Put **all tables, constants, and equations** into a versioned “calc pack” (YAML files for humans, loaded into memory as JSON). The application:

1. Loads the calc pack
2. Validates schema + expressions + units + dependencies
3. Builds a dependency graph
4. Evaluates in a safe sandbox
5. Exposes results, warnings, and trace steps to the UI
6. Allows edits, re-validates, hot-reloads if valid, rejects if not

## Recommended file structure
```
calc_pack/
  manifest.yml
  domains/
    electrical.yml
  tables/
    demand_factors.yml
    motor_tables.yml
  units.yml
  schemas/
    calc_pack.schema.json
```

## How evaluation works

### 1) Build a dependency graph

Parse each `computed.expr`, extract referenced identifiers, then topologically sort:

* `demand_kva` depends on `connected_kva`, `demand_factor`
* `service_amps` depends on `demand_kva`, `phase`, `service_voltage`, `sqrt3`

If there is a cycle, reject the pack with a clear message: “Cycle detected: A -> B -> A”.

### 2) Safe expression parsing

Do not `eval`. Parse expressions into an AST and only allow:

* numeric literals, strings for enums
* identifiers that exist in `inputs/constants/computed/lookups outputs`
* approved operators and functions

### 3) Lookups resolved as data operations

Lookup nodes call your table engine with:

* key fields match (category)
* compute factor by interpolation/exact match
* return warnings (extrapolation, missing category)

### 4) Produce a trace for UX

For every computed value, store:

* formula string
* resolved inputs
* intermediate lookup details
* warnings/errors
  This makes the UI feel “engineered” instead of a black box.

## Hot-reload and “edit in app” without crashing

When a user edits equations or table values in the GUI:

1. Apply changes to an in-memory copy of the calc pack
2. Run validation pipeline:

   * schema validation
   * identifier validation
   * expression parse validation
   * dependency graph validation
   * unit compatibility checks
   * run a small “smoke evaluation” with current inputs
3. If valid: swap the active pack atomically
4. If invalid: reject change and show exact error location, keep last known-good pack active

Also keep:

* `last_known_good_pack` cached on disk
* automatic backup of every accepted change as a new version or timestamped patch

## Validation rules you want (practical set)

* **No unknown identifiers** in expressions
* **No cycles** in computed dependencies
* **All lookups resolvable** (table exists, output field exists)
* **All enums validated** (phase must be “1” or “3”)
* **No division by zero** possible in common paths (you can statically warn if denominator references a user input without constraints)
* **Interpolation bounds behavior defined** (error, clamp, allow-extrapolate-with-warning)
* **Unit tags present** on every computed output

## Minimal “calc engine” responsibilities

* Load YAML
* Validate (JSON Schema + custom rules)
* Compile expressions to safe executable form
* Evaluate with an input dict
* Emit:

  * results dict
  * warnings list
  * errors list
  * trace dict (per computed item)

## UI implications (what this unlocks)

* A “Formula Editor” panel that shows:

  * expression
  * referenced variables
  * unit
  * last evaluation value
  * validation errors with line/column if you keep source mapping
* A “Tables” editor with graph preview for piecewise curves
* A “Diff” view between pack versions (what changed vs last good)

## How to migrate from the existing Excel

* Identify each Excel output cell you care about as a `computed` item
* Identify each Excel input cell as an `input`
* Move any hidden constants into `constants`
* Convert VLOOKUPs and tables into `tables/*.yml`
* Run a regression harness: same inputs, compare Excel outputs vs engine outputs, store tolerances

If you want, paste one representative slice of your current Excel logic, just the inputs and 5 to 10 key outputs, and I’ll translate it into a first-pass calc pack layout like the above with the dependency graph spelled out.
