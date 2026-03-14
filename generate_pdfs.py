#!/usr/bin/env python3
"""Generate PDF user guides from .txt source files for CleanScan."""

import os
import shutil
from fpdf import FPDF

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
GUIDE_DIR = os.path.join(BASE_DIR, "Users Guide")
SOURCE_FR = os.path.join(BASE_DIR, "UserGuide.txt")

FONTS_DIR = "C:/Windows/Fonts"
FONT_REGULAR = os.path.join(FONTS_DIR, "consola.ttf")
FONT_BOLD    = os.path.join(FONTS_DIR, "consolab.ttf")
FONT_ITALIC  = os.path.join(FONTS_DIR, "consolai.ttf")

GUIDES = {
    "fr": os.path.join(GUIDE_DIR, "CleanScan_Guide_fr.txt"),
    "en": os.path.join(GUIDE_DIR, "CleanScan_Guide_en.txt"),
    "de": os.path.join(GUIDE_DIR, "CleanScan_Guide_de.txt"),
    "es": os.path.join(GUIDE_DIR, "CleanScan_Guide_es.txt"),
}


class GuidePDF(FPDF):
    def header(self):
        self.set_font("Consolas", "I", 7)
        self.set_text_color(120, 120, 120)
        self.cell(0, 4, "CleanScan \u2014 ScanFilm SNC \u2014 www.scanfilm.ch",
                  align="R", new_x="LMARGIN", new_y="NEXT")
        self.ln(2)

    def footer(self):
        self.set_y(-12)
        self.set_font("Consolas", "I", 7)
        self.set_text_color(120, 120, 120)
        self.cell(0, 10, f"\u2014 {self.page_no()} \u2014", align="C")


def generate_pdf(txt_path: str, pdf_path: str):
    with open(txt_path, "r", encoding="utf-8") as f:
        lines = f.readlines()

    pdf = GuidePDF(orientation="P", unit="mm", format="A4")

    # Register Consolas (Unicode TTF)
    pdf.add_font("Consolas", "",  FONT_REGULAR, uni=True)
    pdf.add_font("Consolas", "B", FONT_BOLD,    uni=True)
    pdf.add_font("Consolas", "I", FONT_ITALIC,  uni=True)

    pdf.set_auto_page_break(auto=True, margin=15)
    pdf.add_page()

    font_size = 8.5
    line_height = 3.8

    for raw_line in lines:
        line = raw_line.rstrip("\n\r")
        stripped = line.strip()

        # Section headers (=== lines)
        if stripped.startswith("===="):
            pdf.set_font("Consolas", "B", font_size)
            pdf.set_text_color(60, 80, 140)
            pdf.cell(0, line_height, line, new_x="LMARGIN", new_y="NEXT")
            pdf.set_text_color(0, 0, 0)
            continue

        # Sub-section dividers (--- lines)
        if stripped.startswith("----"):
            pdf.set_font("Consolas", "", font_size - 1)
            pdf.set_text_color(140, 140, 140)
            pdf.cell(0, line_height, line, new_x="LMARGIN", new_y="NEXT")
            pdf.set_text_color(0, 0, 0)
            continue

        # Main title
        if "C L E A N S C A N" in line:
            pdf.set_font("Consolas", "B", 14)
            pdf.set_text_color(30, 50, 100)
            pdf.cell(0, 7, line, new_x="LMARGIN", new_y="NEXT")
            pdf.set_font("Consolas", "", font_size)
            pdf.set_text_color(0, 0, 0)
            continue

        # Section number titles (e.g. "1.  PRESENTATION")
        if stripped and len(stripped) > 3 and stripped[0].isdigit() and "." in stripped[:4]:
            rest = stripped.split(".", 1)[1].strip() if "." in stripped else stripped
            if rest and rest == rest.upper() and any(c.isalpha() for c in rest):
                pdf.set_font("Consolas", "B", font_size + 1)
                pdf.set_text_color(30, 50, 100)
                pdf.cell(0, line_height + 0.5, line, new_x="LMARGIN", new_y="NEXT")
                pdf.set_font("Consolas", "", font_size)
                pdf.set_text_color(0, 0, 0)
                continue

        # Sub-section titles (ALL CAPS lines, at least 8 chars, not indented much)
        if (stripped and len(stripped) > 8
                and stripped == stripped.upper()
                and any(c.isalpha() for c in stripped)
                and not stripped.startswith("---")
                and not stripped.startswith("===")
                and len(line) - len(line.lstrip()) < 6):
            pdf.set_font("Consolas", "B", font_size)
            pdf.set_text_color(50, 70, 120)
            pdf.cell(0, line_height + 0.3, line, new_x="LMARGIN", new_y="NEXT")
            pdf.set_font("Consolas", "", font_size)
            pdf.set_text_color(0, 0, 0)
            continue

        # Empty lines
        if not stripped:
            pdf.ln(line_height * 0.6)
            continue

        # Normal text
        pdf.set_font("Consolas", "", font_size)
        pdf.set_text_color(0, 0, 0)
        pdf.cell(0, line_height, line, new_x="LMARGIN", new_y="NEXT")

    pdf.output(pdf_path)
    print(f"  OK: {pdf_path}")


def main():
    os.makedirs(GUIDE_DIR, exist_ok=True)

    # Copy FR source to guide dir
    fr_dest = GUIDES["fr"]
    if os.path.abspath(SOURCE_FR) != os.path.abspath(fr_dest):
        shutil.copy2(SOURCE_FR, fr_dest)
        print(f"  Copied FR source -> {fr_dest}")

    for lang, txt_path in GUIDES.items():
        if not os.path.exists(txt_path):
            print(f"  SKIP: {txt_path} (not found)")
            continue
        pdf_path = txt_path.replace(".txt", ".pdf")
        generate_pdf(txt_path, pdf_path)

    print("\nDone! PDFs generated in:", GUIDE_DIR)


if __name__ == "__main__":
    main()
