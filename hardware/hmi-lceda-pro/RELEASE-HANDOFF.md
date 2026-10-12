# LCEDA-Pro HMI R1 handoff package

工程：`HMI_T113_SZCLX070H_R1`

## 使用顺序

1. 阅读 `DESIGN-REVIEW.md` 和 `README.md`。
2. 使用 `project.json` 确认源文件版本。
3. 在 LCEDA-Pro 创建 9 页工程。
4. 按 `capture-package/P1..P9.md` 或对应 CSV 录入。
5. 使用 `lcd-t113-pin-handoff.md` 逐脚核对 LCD 50PIN。
6. 使用 `normalized-netlist.csv` 检查每个压缩网络是否已经逐条录入。
7. 使用 `bom-review.csv` 和 `footprints.csv` 绑定器件与封装。
8. 在 LCEDA-Pro 内运行 ERC，保存工程并导出网表/BOM。
9. 重新运行本目录的审计脚本，确认源矩阵未被意外修改。

## 自动检查

```bash
python3 generate_pin_handoff.py
python3 generate_netlist.py
python3 audit_connectivity.py
python3 validate_project.py
python3 generate_capture_package.py
python3 generate_bom.py
python3 generate_lceda.py
python3 generate_release_manifest.py
```

预期关键结果：

```text
pin handoff: PASS (50 panel pins, 0 issues)
connectivity audit: PASS (0 issues)
project validated: ... 50 pin handoff rows, 50 LCD pins, 38 SoC assignments, 9 pages
LCD power/interface baseline validated
```

## 重要限制

本目录不是 LCEDA-Pro 的原生专有工程文件。它是可复现、可审计的原理图录入源包。原生 LCEDA-Pro 工程必须在 LCEDA-Pro GUI 中创建、保存、ERC 和导出；在此之前不得宣称设计已达到投板状态。

`RELEASE-MANIFEST.sha256` 用于确认交接包文件没有被混用或意外修改。
