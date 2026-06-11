import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Scoreboard } from "../../components/Scoreboard";
import type { ScoredResult } from "../../types/game";

const choiceMap: Record<string, string> = {
  "1": "rock",
  "2": "paper",
};

const entry = (result: ScoredResult["results"], id: string): ScoredResult => ({
  id,
  results: result,
  player: 2,
  computer: 1,
});

describe("Scoreboard", () => {
  it("shows empty state when there are no results", () => {
    render(<Scoreboard scoreboard={[]} disabled={false} onReset={vi.fn()} choiceMap={choiceMap} />);
    expect(screen.getByText("No games played yet.")).toBeInTheDocument();
  });

  it("renders a row for each scoreboard entry", () => {
    const board = [entry("win", "1"), entry("lose", "2"), entry("tie", "3")];
    render(<Scoreboard scoreboard={board} disabled={false} onReset={vi.fn()} choiceMap={choiceMap} />);
    expect(screen.getByText("WIN")).toBeInTheDocument();
    expect(screen.getByText("LOSE")).toBeInTheDocument();
    expect(screen.getByText("TIE")).toBeInTheDocument();
  });

  it("applies the correct class to each row", () => {
    const board = [entry("win", "1")];
    const { container } = render(
      <Scoreboard scoreboard={board} disabled={false} onReset={vi.fn()} choiceMap={choiceMap} />
    );
    expect(container.querySelector(".score-item--win")).toBeInTheDocument();
  });

  it("reset button is disabled when scoreboard is empty", () => {
    render(<Scoreboard scoreboard={[]} disabled={false} onReset={vi.fn()} choiceMap={choiceMap} />);
    expect(screen.getByRole("button", { name: /reset/i })).toBeDisabled();
  });

  it("reset button is disabled when disabled prop is true", () => {
    const board = [entry("win", "1")];
    render(<Scoreboard scoreboard={board} disabled={true} onReset={vi.fn()} choiceMap={choiceMap} />);
    expect(screen.getByRole("button", { name: /reset/i })).toBeDisabled();
  });

  it("calls onReset when the reset button is clicked", async () => {
    const onReset = vi.fn();
    const board = [entry("win", "1")];
    render(<Scoreboard scoreboard={board} disabled={false} onReset={onReset} choiceMap={choiceMap} />);

    await userEvent.click(screen.getByRole("button", { name: /reset/i }));

    expect(onReset).toHaveBeenCalledOnce();
  });
});
