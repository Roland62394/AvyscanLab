#!/usr/bin/env python3
"""Generate professional PDF user guides for AvyScanLab from .txt sources."""

import os
import re
import warnings
from fpdf import FPDF

warnings.filterwarnings("ignore", category=DeprecationWarning)

# Replace emojis that most PDF fonts can't render
EMOJI_REPLACEMENTS = {
    "\U0001f441": "[eye]",      # 👁
    "\U0001f4ac": "[tip]",      # 💬
    "\U0001f4ca": "[chart]",    # 📊
    "\U0001f4c2": "[dir]",      # 📂
    "\u23fa":     "[rec]",      # ⏺
    "\u23f9":     "[stop]",     # ⏹
}

GUIDE_DIR = os.path.dirname(os.path.abspath(__file__))
LOGO_PATH = os.path.join(os.path.dirname(GUIDE_DIR), "Assets", "Logo.png")

# Brand colours
BRAND_BLUE   = (30, 60, 114)
BRAND_DARK   = (35, 35, 40)
ACCENT_GREEN = (53, 193, 86)
LIGHT_BG     = (245, 245, 248)
WHITE        = (255, 255, 255)
GREY_TEXT    = (100, 100, 110)
BLACK        = (30, 30, 30)

LANGS = {
    "fr": "AvyScanLab_Guide_fr.txt",
    "en": "AvyScanLab_Guide_en.txt",
    "de": "AvyScanLab_Guide_de.txt",
    "es": "AvyScanLab_Guide_es.txt",
}


class GuidePDF(FPDF):
    def __init__(self):
        super().__init__(orientation="P", unit="mm", format="A4")
        self.set_auto_page_break(auto=True, margin=20)
        # Register DejaVu for Unicode support (emoji, arrows, etc.)
        font_dir = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Microsoft", "Windows", "Fonts")
        system_font_dir = "C:/Windows/Fonts"
        # Try to find a Unicode-capable font
        dejavu = None
        for d in [font_dir, system_font_dir]:
            candidate = os.path.join(d, "DejaVuSans.ttf")
            if os.path.exists(candidate):
                dejavu = candidate
                break
        if dejavu:
            self.add_font("DejaVu", "", dejavu)
            dv_bold = dejavu.replace("DejaVuSans.ttf", "DejaVuSans-Bold.ttf")
            if os.path.exists(dv_bold):
                self.add_font("DejaVu", "B", dv_bold)
            else:
                self.add_font("DejaVu", "B", dejavu)
            self.unicode_font = "DejaVu"
        else:
            self.unicode_font = None
        self._page_title = ""

    def _use_font(self, style="", size=10):
        if self.unicode_font:
            self.set_font(self.unicode_font, style, size)
        else:
            self.set_font("Helvetica", style, size)

    def header(self):
        if self.page_no() == 1:
            return  # Cover page has custom header
        self.set_fill_color(*BRAND_BLUE)
        self.rect(0, 0, 210, 8, "F")
        self._use_font("", 7)
        self.set_text_color(*WHITE)
        self.set_y(1.5)
        self.cell(0, 5, "AVYSCAN LAB  |  ScanFilm SNC  |  www.scanfilm.ch", align="C")
        self.set_text_color(*BLACK)
        self.set_y(14)

    def footer(self):
        if self.page_no() == 1:
            return
        self.set_y(-12)
        self.set_draw_color(*BRAND_BLUE)
        self.line(15, self.get_y(), 195, self.get_y())
        self._use_font("", 7)
        self.set_text_color(*GREY_TEXT)
        self.cell(0, 8, f"— {self.page_no() - 1} —", align="C")


def make_cover(pdf: GuidePDF, lines: list[str]):
    """Build a professional cover page from the first few lines of the guide."""
    pdf.add_page()

    # Full-page brand background band
    pdf.set_fill_color(*BRAND_BLUE)
    pdf.rect(0, 0, 210, 120, "F")

    # Logo
    if os.path.exists(LOGO_PATH):
        pdf.image(LOGO_PATH, x=80, y=18, w=50)
        y_after_logo = 75
    else:
        y_after_logo = 40

    # Title
    pdf._use_font("B", 28)
    pdf.set_text_color(*WHITE)
    pdf.set_y(y_after_logo)
    pdf.cell(0, 14, "AVYSCAN LAB", align="C", new_x="LMARGIN", new_y="NEXT")

    # Subtitle (Guide de l'utilisateur / User Guide / etc.)
    subtitle = ""
    for l in lines[:5]:
        stripped = l.strip()
        if stripped and "AVYSCAN" not in stripped.upper() and stripped != "":
            subtitle = stripped
            break
    pdf._use_font("", 14)
    pdf.set_text_color(200, 210, 230)
    pdf.cell(0, 10, subtitle, align="C", new_x="LMARGIN", new_y="NEXT")

    # Company info
    pdf.set_y(105)
    pdf._use_font("", 10)
    pdf.set_text_color(180, 190, 210)
    pdf.cell(0, 6, "ScanFilm SNC  —  www.scanfilm.ch", align="C", new_x="LMARGIN", new_y="NEXT")

    # Trial version info
    pdf.set_y(140)
    pdf.set_fill_color(*LIGHT_BG)
    pdf.rect(20, 135, 170, 24, "F")
    pdf._use_font("", 9)
    pdf.set_text_color(*GREY_TEXT)
    for l in lines[:10]:
        stripped = l.strip()
        if "version" in stripped.lower() or "release" in stripped.lower():
            pdf.set_y(139)
            pdf.cell(0, 6, stripped, align="C", new_x="LMARGIN", new_y="NEXT")
        elif ("fichier" in stripped.lower() or "file" in stripped.lower()
              or "datei" in stripped.lower() or "archivo" in stripped.lower()):
            pdf.cell(0, 6, stripped, align="C", new_x="LMARGIN", new_y="NEXT")


def is_section_header(line: str) -> bool:
    return bool(re.match(r"^={10,}", line.strip()))


def is_subsection_header(line: str) -> bool:
    stripped = line.strip()
    return bool(re.match(r"^-{20,}", stripped))


def is_dotted_header(line: str) -> bool:
    return bool(re.match(r"^\.{10,}", line.strip()))


def parse_section_title(line: str) -> str | None:
    m = re.match(r"^\s*(\d+)\.\s+(.+)$", line.strip())
    if m:
        return f"{m.group(1)}.  {m.group(2)}"
    # Non-numbered section titles (REMERCIEMENTS, etc.)
    stripped = line.strip()
    if stripped and stripped == stripped.upper() and len(stripped) > 5 and not stripped.startswith("="):
        return stripped
    return None


def render_content(pdf: GuidePDF, txt_path: str):
    with open(txt_path, "r", encoding="utf-8") as f:
        lines = f.readlines()

    # Replace problematic emojis
    for idx, line in enumerate(lines):
        for emoji, replacement in EMOJI_REPLACEMENTS.items():
            if emoji in line:
                lines[idx] = lines[idx].replace(emoji, replacement)

    # Cover page
    make_cover(pdf, lines)

    # Find where actual content starts (after the first = block and TOC)
    i = 0
    # Skip header lines (title, company, version)
    while i < len(lines) and not is_section_header(lines[i]):
        i += 1

    # Now process sections
    in_toc = False
    prev_was_separator = False

    while i < len(lines):
        line = lines[i]
        raw = line.rstrip("\n")
        stripped = raw.strip()

        # Skip pure separator lines
        if is_section_header(raw):
            prev_was_separator = True
            i += 1
            continue

        # Section title (line right after ===)
        if prev_was_separator and stripped:
            prev_was_separator = False

            # Check if this is the TOC
            if "TABLE" in stripped.upper() or "INHALTS" in stripped.upper() or "ÍNDICE" in stripped.upper():
                in_toc = True
                pdf.add_page()
                pdf._use_font("B", 16)
                pdf.set_text_color(*BRAND_BLUE)
                pdf.cell(0, 12, stripped, align="C", new_x="LMARGIN", new_y="NEXT")
                pdf.ln(4)
                i += 1
                continue

            # Check for the bottom "A V Y S C A N  L A B" section
            if "A V Y" in stripped or "A V Y S C A N" in stripped:
                # Footer section - render as centered text
                pdf.ln(10)
                pdf.set_fill_color(*BRAND_BLUE)
                pdf.rect(15, pdf.get_y(), 180, 30, "F")
                pdf._use_font("B", 14)
                pdf.set_text_color(*WHITE)
                pdf.set_y(pdf.get_y() + 4)
                pdf.cell(0, 8, "AVYSCAN LAB", align="C", new_x="LMARGIN", new_y="NEXT")
                pdf._use_font("", 9)
                pdf.cell(0, 5, "ScanFilm SNC  —  www.scanfilm.ch", align="C", new_x="LMARGIN", new_y="NEXT")
                # Skip remaining lines
                i += 1
                while i < len(lines):
                    s = lines[i].strip()
                    if s and "===" not in s:
                        pdf.set_text_color(200, 210, 230)
                        pdf._use_font("", 8)
                        pdf.cell(0, 5, s, align="C", new_x="LMARGIN", new_y="NEXT")
                    i += 1
                break

            in_toc = False

            # Regular section title
            title = parse_section_title(stripped) or stripped
            pdf.add_page()

            # Section title band
            pdf.set_fill_color(*BRAND_BLUE)
            pdf.rect(15, pdf.get_y() - 2, 180, 12, "F")
            pdf._use_font("B", 13)
            pdf.set_text_color(*WHITE)
            pdf.cell(0, 10, f"  {title}", new_x="LMARGIN", new_y="NEXT")
            pdf.ln(6)
            pdf.set_text_color(*BLACK)
            i += 1
            continue

        prev_was_separator = False

        # TOC lines
        if in_toc:
            if stripped:
                pdf._use_font("", 10)
                pdf.set_text_color(*BLACK)
                pdf.cell(0, 6, f"    {stripped}", new_x="LMARGIN", new_y="NEXT")
            else:
                pdf.ln(2)
            i += 1
            continue

        # Subsection separator ---
        if is_subsection_header(raw):
            i += 1
            continue

        # Dotted separator ...
        if is_dotted_header(raw):
            i += 1
            continue

        # Subsection title (ALL CAPS line, or line with specific patterns)
        if (stripped and stripped == stripped.upper() and len(stripped) > 8
                and not stripped.startswith("-") and not stripped.startswith("=")
                and not stripped.startswith(".") and not stripped.startswith("↻")
                and not stripped.startswith("▶") and "===" not in stripped
                and any(c.isalpha() for c in stripped)):
            # Check if it's really a subsection title (not a parameter line)
            if not any(c.isdigit() for c in stripped[:5]) or "—" in stripped:
                pdf.ln(4)
                pdf.set_fill_color(*LIGHT_BG)
                y = pdf.get_y()
                pdf.rect(15, y, 180, 8, "F")
                pdf.set_draw_color(*BRAND_BLUE)
                pdf.line(15, y, 15, y + 8)
                pdf.line(15.5, y, 15.5, y + 8)
                pdf._use_font("B", 10)
                pdf.set_text_color(*BRAND_DARK)
                pdf.cell(0, 8, f"   {stripped}", new_x="LMARGIN", new_y="NEXT")
                pdf.ln(2)
                pdf.set_text_color(*BLACK)
                i += 1
                continue

        # Empty line
        if not stripped:
            pdf.ln(2)
            i += 1
            continue

        # Regular text line
        # Detect indentation level
        indent = len(raw) - len(raw.lstrip())
        left_margin = 18 + min(indent * 0.8, 40)

        pdf._use_font("", 9)
        pdf.set_text_color(*BLACK)
        pdf.set_x(left_margin)

        # Handle long lines with multi_cell
        available_width = 195 - left_margin
        pdf.multi_cell(available_width, 4.5, stripped, new_x="LMARGIN", new_y="NEXT")

        i += 1


def generate_pdf(lang: str, txt_file: str):
    txt_path = os.path.join(GUIDE_DIR, txt_file)
    if not os.path.exists(txt_path):
        print(f"  SKIP {txt_file} (not found)")
        return

    pdf_file = txt_file.replace(".txt", ".pdf")
    pdf_path = os.path.join(GUIDE_DIR, pdf_file)

    pdf = GuidePDF()
    render_content(pdf, txt_path)
    pdf.output(pdf_path)
    print(f"  OK   {pdf_file}")


def main():
    print("Generating AvyScanLab PDF guides...")
    for lang, txt_file in LANGS.items():
        generate_pdf(lang, txt_file)
    print("Done.")


if __name__ == "__main__":
    main()
