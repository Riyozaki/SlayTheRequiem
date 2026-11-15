"""Мини-набор для векторных спрайтов врагов (art/monsters/*.svg → images/monsters/*.png).

Стиль — docs/plan/art-ui-animation-spec.md §1 и §3.9: толстый тёмный контур, 2–3 тона заливки с градиентом света
сверху-слева, цветной контровой свет акта, светящиеся глаза/руны (размытие), лёгкое «зерно» по силуэту.
Здесь только примитивы; сами существа — в draw_monsters.py.
"""
from __future__ import annotations

import math

INK = "#120d14"


class Svg:
    def __init__(self, w: int, h: int):
        self.w, self.h = w, h
        self.defs: list[str] = []
        self.body: list[str] = []
        self._ids: set[str] = set()
        self._n = 0

    # ───── defs ─────
    def uid(self, prefix: str) -> str:
        self._n += 1
        return f"{prefix}{self._n}"

    def lin(self, stops, x1=0, y1=0, x2=0, y2=1, gid=None) -> str:
        gid = gid or self.uid("l")
        s = _stop_xml(stops)
        self.defs.append(f'<linearGradient id="{gid}" x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}">{s}</linearGradient>')
        return f"url(#{gid})"

    def rad(self, stops, cx=0.5, cy=0.5, r=0.5, fx=None, fy=None, gid=None, user=False) -> str:
        gid = gid or self.uid("r")
        s = _stop_xml(stops)
        units = ' gradientUnits="userSpaceOnUse"' if user else ""
        f = f' fx="{fx}" fy="{fy}"' if fx is not None else ""
        self.defs.append(f'<radialGradient id="{gid}" cx="{cx}" cy="{cy}" r="{r}"{f}{units}>{s}</radialGradient>')
        return f"url(#{gid})"

    def blur(self, sd: float, fid=None) -> str:
        fid = fid or self.uid("b")
        self.defs.append(f'<filter id="{fid}" x="-50%" y="-50%" width="200%" height="200%">'
                         f'<feGaussianBlur stdDeviation="{sd}"/></filter>')
        return f"url(#{fid})"

    def glow(self, sd: float, fid=None) -> str:
        """Свечение: размытая копия под оригиналом."""
        fid = fid or self.uid("g")
        self.defs.append(f'<filter id="{fid}" x="-80%" y="-80%" width="260%" height="260%">'
                         f'<feGaussianBlur in="SourceGraphic" stdDeviation="{sd}" result="b"/>'
                         f'<feMerge><feMergeNode in="b"/><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge></filter>')
        return f"url(#{fid})"

    def grain(self, strength=0.22, freq=0.9, seed=3, fid=None) -> str:
        """Зерно по силуэту группы: шум умножается на картинку, прозрачность сохраняется."""
        fid = fid or self.uid("n")
        self.defs.append(
            f'<filter id="{fid}" x="0" y="0" width="100%" height="100%" color-interpolation-filters="sRGB">'
            f'<feTurbulence type="fractalNoise" baseFrequency="{freq}" numOctaves="2" seed="{seed}" result="t"/>'
            f'<feColorMatrix in="t" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 {strength} 0" result="d"/>'
            f'<feComposite in="d" in2="SourceAlpha" operator="in" result="m"/>'
            f'<feMerge><feMergeNode in="SourceGraphic"/><feMergeNode in="m"/></feMerge></filter>')
        return f"url(#{fid})"

    # ───── shapes ─────
    def add(self, s: str):
        self.body.append(s)

    def path(self, d, fill="none", stroke=INK, sw=4.0, extra=""):
        st = f' stroke="{stroke}" stroke-width="{sw}" stroke-linejoin="round" stroke-linecap="round"' if stroke else ""
        self.body.append(f'<path d="{d}" fill="{fill}"{st} {extra}/>')

    def ellipse(self, cx, cy, rx, ry, fill, stroke=None, sw=0, extra=""):
        st = f' stroke="{stroke}" stroke-width="{sw}"' if stroke else ""
        self.body.append(f'<ellipse cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}" fill="{fill}"{st} {extra}/>')

    def circle(self, cx, cy, r, fill, stroke=None, sw=0, extra=""):
        st = f' stroke="{stroke}" stroke-width="{sw}"' if stroke else ""
        self.body.append(f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="{fill}"{st} {extra}/>')

    def open(self, extra=""):
        self.body.append(f"<g {extra}>")

    def close(self):
        self.body.append("</g>")

    def shadow(self, cx, cy, rx, ry, opacity=0.45):
        f = self.blur(6)
        self.ellipse(cx, cy, rx, ry, "#000", extra=f'opacity="{opacity}" filter="{f}"')

    def eye(self, cx, cy, r, color, core="#ffffff"):
        g = self.glow(r * 0.9)
        self.circle(cx, cy, r * 2.2, color, extra=f'opacity="0.35" filter="{self.blur(r * 1.4)}"')
        self.circle(cx, cy, r, color, extra=f'filter="{g}"')
        self.circle(cx, cy, r * 0.45, core)

    def render(self) -> str:
        return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.w}" height="{self.h}" viewBox="0 0 {self.w} {self.h}">'
                f'<defs>{"".join(self.defs)}</defs>{"".join(self.body)}</svg>')


def _stop_xml(stops) -> str:
    out = []
    for o, c, a in _stops(stops):
        op = "" if a is None else f' stop-opacity="{a}"'
        out.append(f'<stop offset="{o}" stop-color="{c}"{op}/>')
    return "".join(out)


def _stops(stops):
    for st in stops:
        if len(st) == 2:
            yield st[0], st[1], None
        else:
            yield st


def mirror(points, cx):
    """Зеркальная копия списка точек относительно вертикали x = cx (для симметричных фигур)."""
    return [(2 * cx - x, y) for x, y in points]


def poly(points, closed=True) -> str:
    d = "M" + " L".join(f"{x:.1f},{y:.1f}" for x, y in points)
    return d + (" Z" if closed else "")


def smooth(points, closed=True, k=1.0) -> str:
    """Сглаженный контур через точки (Catmull-Rom → кубические Безье)."""
    n = len(points)
    if n < 3:
        return poly(points, closed)

    def at(i):
        if closed:
            return points[i % n]
        return points[max(0, min(n - 1, i))]

    d = f"M{points[0][0]:.1f},{points[0][1]:.1f}"
    for i in range(n if closed else n - 1):
        p0, p1, p2, p3 = at(i - 1), at(i), at(i + 1), at(i + 2)
        c1 = (p1[0] + (p2[0] - p0[0]) * k / 6, p1[1] + (p2[1] - p0[1]) * k / 6)
        c2 = (p2[0] - (p3[0] - p1[0]) * k / 6, p2[1] - (p3[1] - p1[1]) * k / 6)
        d += f" C{c1[0]:.1f},{c1[1]:.1f} {c2[0]:.1f},{c2[1]:.1f} {p2[0]:.1f},{p2[1]:.1f}"
    return d + (" Z" if closed else "")


def star(cx, cy, r1, r2, n=5, rot=-90) -> str:
    pts = []
    for i in range(n * 2):
        r = r1 if i % 2 == 0 else r2
        a = math.radians(rot + i * 180 / n)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return poly(pts)


def arc_pts(cx, cy, rx, ry, a0, a1, steps=24):
    return [(cx + rx * math.cos(math.radians(a0 + (a1 - a0) * i / steps)),
             cy + ry * math.sin(math.radians(a0 + (a1 - a0) * i / steps))) for i in range(steps + 1)]
