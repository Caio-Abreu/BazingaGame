import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ChoiceGrid } from "../../components";

const choices = [
  { id: 1, name: "rock" },
  { id: 2, name: "paper" },
  { id: 3, name: "scissors" },
];

describe("ChoiceGrid", () => {
  it("renders a button for each choice", () => {
    render(<ChoiceGrid choices={choices} disabled={false} onSelect={vi.fn()} />);
    expect(screen.getAllByRole("button")).toHaveLength(3);
    expect(screen.getByText("rock")).toBeInTheDocument();
    expect(screen.getByText("paper")).toBeInTheDocument();
    expect(screen.getByText("scissors")).toBeInTheDocument();
  });

  it("calls onSelect with the choice id when clicked", async () => {
    const onSelect = vi.fn();
    render(<ChoiceGrid choices={choices} disabled={false} onSelect={onSelect} />);

    await userEvent.click(screen.getByText("rock"));

    expect(onSelect).toHaveBeenCalledOnce();
    expect(onSelect).toHaveBeenCalledWith(1);
  });

  it("disables all buttons when disabled=true", () => {
    render(<ChoiceGrid choices={choices} disabled={true} onSelect={vi.fn()} />);
    screen.getAllByRole("button").forEach((btn) => {
      expect(btn).toBeDisabled();
    });
  });

  it("renders no buttons for an empty choices list", () => {
    render(<ChoiceGrid choices={[]} disabled={false} onSelect={vi.fn()} />);
    expect(screen.queryAllByRole("button")).toHaveLength(0);
  });
});
