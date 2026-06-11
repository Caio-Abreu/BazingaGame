import { describe, it, expect } from "vitest";
import { choiceIcon, ICONS, ICON_FALLBACK } from "../../constants/game";

describe("choiceIcon", () => {
  it("returns the correct icon for each known choice", () => {
    Object.entries(ICONS).forEach(([name, icon]) => {
      expect(choiceIcon(name)).toBe(icon);
    });
  });

  it("returns the fallback icon for an unknown choice name", () => {
    expect(choiceIcon("unknown")).toBe(ICON_FALLBACK);
  });

  it("returns the fallback icon for undefined", () => {
    expect(choiceIcon(undefined)).toBe(ICON_FALLBACK);
  });

  it("returns the fallback icon for an empty string", () => {
    expect(choiceIcon("")).toBe(ICON_FALLBACK);
  });
});
