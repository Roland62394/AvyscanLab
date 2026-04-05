#!/usr/bin/env python3
"""Generate a styled French user manual PDF without external dependencies."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import textwrap

ROOT = Path(__file__).resolve().parent
TXT_PATH = ROOT / "Users Guide" / "Mode_emploi_AvyScanLab_fr.txt"
PDF_PATH = ROOT / "Users Guide" / "Mode_emploi_AvyScanLab_fr.pdf"


@dataclass
class PdfPage:
    content: bytearray


class SimplePdf:
    def __init__(self) -> None:
        self.pages: list[PdfPage] = []
        self.page_w = 595.28  # A4 points
        self.page_h = 841.89

    @staticmethod
    def _esc(text: str) -> str:
        return text.replace('\\', r'\\').replace('(', r'\(').replace(')', r'\)')

    def _new_page(self) -> PdfPage:
        page = PdfPage(bytearray())
        self.pages.append(page)
        return page

    def add_manual(self, lines: list[str]) -> None:
        page = self._new_page()
        y = 790.0
        left = 50.0

        def draw_text(txt: str, x: float, yy: float, font: str = "F1", size: int = 11) -> None:
            safe = self._esc(txt)
            page.content.extend(f"BT /{font} {size} Tf 0 0 0 rg 1 0 0 1 {x:.2f} {yy:.2f} Tm ({safe}) Tj ET\n".encode("cp1252", errors="replace"))

        def draw_line(x1: float, y1: float, x2: float, y2: float, width: float = 1.0, rgb=(0.2, 0.33, 0.55)) -> None:
            r, g, b = rgb
            page.content.extend(f"{r:.3f} {g:.3f} {b:.3f} RG {width:.2f} w {x1:.2f} {y1:.2f} m {x2:.2f} {y2:.2f} l S\n".encode())

        def draw_rect(x: float, yy: float, w: float, h: float, rgb=(0.92, 0.95, 1.0)) -> None:
            r, g, b = rgb
            page.content.extend(f"{r:.3f} {g:.3f} {b:.3f} rg {x:.2f} {yy:.2f} {w:.2f} {h:.2f} re f\n".encode())

        # Cover banner
        draw_rect(35, 740, 525, 75, rgb=(0.90, 0.94, 1.0))
        draw_text("AVYSCAN LAB", 55, 785, font="F2", size=26)
        draw_text("Mode d'emploi professionnel — Français", 55, 760, font="F1", size=13)
        draw_text("Edition du 23 mars 2026", 55, 744, font="F1", size=10)
        y = 710

        for raw in lines:
            line = raw.rstrip("\n")
            if not line.strip():
                y -= 8
            elif set(line.strip()) == {"="}:
                draw_line(left, y + 2, self.page_w - left, y + 2, width=1.2)
                y -= 10
            elif line.strip().startswith(tuple(str(i) + ")" for i in range(1, 10))) or any(line.strip().startswith(f"{i})") for i in range(10, 30)):
                y -= 3
                draw_text(line.strip(), left, y, font="F2", size=13)
                y -= 15
            elif line.strip().startswith("-") or line.strip().startswith("["):
                wrapped = textwrap.wrap(line, width=95) or [line]
                for w in wrapped:
                    draw_text(w, left + 10, y, font="F1", size=10)
                    y -= 12
            else:
                wrapped = textwrap.wrap(line, width=98) or [line]
                for w in wrapped:
                    draw_text(w, left, y, font="F1", size=10)
                    y -= 12

            if y < 60:
                draw_text(f"Page {len(self.pages)}", 280, 25, font="F1", size=9)
                page = self._new_page()
                y = 790
                draw_text("AVYSCAN LAB — Mode d'emploi", 50, 810, font="F2", size=12)
                draw_line(50, 804, self.page_w - 50, 804, width=0.8, rgb=(0.75, 0.79, 0.88))

        draw_text(f"Page {len(self.pages)}", 280, 25, font="F1", size=9)

    def save(self, path: Path) -> None:
        objects: list[bytes] = []

        # 1. Catalog, 2. Pages
        objects.append(b"<< /Type /Catalog /Pages 2 0 R >>")

        kids = " ".join(f"{3 + i * 2} 0 R" for i in range(len(self.pages)))
        objects.append(f"<< /Type /Pages /Count {len(self.pages)} /Kids [{kids}] >>".encode())

        # Page + content objects
        for i, p in enumerate(self.pages):
            page_obj_num = 3 + i * 2
            content_obj_num = page_obj_num + 1
            page_dict = (
                f"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {self.page_w:.2f} {self.page_h:.2f}] "
                f"/Resources << /Font << /F1 100 0 R /F2 101 0 R >> >> /Contents {content_obj_num} 0 R >>"
            )
            objects.append(page_dict.encode())
            stream = bytes(p.content)
            content = b"<< /Length " + str(len(stream)).encode() + b" >>\nstream\n" + stream + b"endstream"
            objects.append(content)

        # Font objects
        while len(objects) < 99:
            objects.append(b"<<>>")
        objects.append(b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")      # 100
        objects.append(b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>") # 101

        out = bytearray(b"%PDF-1.4\n")
        offsets = [0]
        for i, obj in enumerate(objects, start=1):
            offsets.append(len(out))
            out.extend(f"{i} 0 obj\n".encode())
            out.extend(obj)
            out.extend(b"\nendobj\n")

        xref_pos = len(out)
        out.extend(f"xref\n0 {len(objects) + 1}\n".encode())
        out.extend(b"0000000000 65535 f \n")
        for off in offsets[1:]:
            out.extend(f"{off:010d} 00000 n \n".encode())

        out.extend(
            f"trailer\n<< /Size {len(objects) + 1} /Root 1 0 R >>\nstartxref\n{xref_pos}\n%%EOF\n".encode()
        )

        path.write_bytes(out)


def main() -> None:
    text = TXT_PATH.read_text(encoding="utf-8")
    lines = text.splitlines()
    pdf = SimplePdf()
    pdf.add_manual(lines)
    pdf.save(PDF_PATH)
    print(f"PDF generated: {PDF_PATH}")


if __name__ == "__main__":
    main()
