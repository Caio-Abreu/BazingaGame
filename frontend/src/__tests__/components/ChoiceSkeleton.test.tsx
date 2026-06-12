import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ChoiceSkeleton } from "../../components";

describe("ChoiceSkeleton", () => {
  it("renders 5 skeleton placeholders", () => {
    const { container } = render(<ChoiceSkeleton />);
    const skeletons = container.querySelectorAll(".choice-btn--skeleton");
    expect(skeletons).toHaveLength(5);
  });

  it("renders no interactive buttons", () => {
    render(<ChoiceSkeleton />);
    expect(screen.queryAllByRole("button")).toHaveLength(0);
  });
});
