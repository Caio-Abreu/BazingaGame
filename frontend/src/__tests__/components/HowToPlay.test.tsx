import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HowToPlay } from "../../components/HowToPlay";

// happy-dom doesn't implement showModal; stub it and mark the dialog open
HTMLDialogElement.prototype.showModal = vi.fn(function (this: HTMLDialogElement) {
  this.setAttribute("open", "");
});

describe("HowToPlay", () => {
  it("renders the dialog with the title", () => {
    render(<HowToPlay onClose={vi.fn()} />);
    expect(screen.getByText("How to play")).toBeInTheDocument();
  });

  it("renders all 10 rules", () => {
    render(<HowToPlay onClose={vi.fn()} />);
    expect(screen.getAllByRole("listitem")).toHaveLength(10);
  });

  it("calls onClose when the close button is clicked", async () => {
    const onClose = vi.fn();
    render(<HowToPlay onClose={onClose} />);
    await userEvent.click(screen.getByRole("button", { name: /close/i }));
    expect(onClose).toHaveBeenCalledOnce();
  });
});
