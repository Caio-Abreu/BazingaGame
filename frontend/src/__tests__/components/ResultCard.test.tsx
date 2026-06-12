import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ResultCard } from "../../components/ResultCard";
import { ChoiceProvider } from "../../context/ChoiceContext";
import type { ScoredResult } from "../../types/game";

const choiceMap: Record<string, string> = {
  "1": "rock",
  "2": "paper",
  "3": "scissors",
};

function renderWithContext(ui: React.ReactElement) {
  return render(<ChoiceProvider choiceMap={choiceMap}>{ui}</ChoiceProvider>);
}

function makeResult(result: ScoredResult["results"], player: number, computer: number): ScoredResult {
  return { id: "test-id", results: result, player, computer };
}

describe("ResultCard", () => {
  it("shows 'You Win!' for a win result", () => {
    renderWithContext(<ResultCard result={makeResult("win", 2, 1)} />);
    expect(screen.getByText("You Win!")).toBeInTheDocument();
  });

  it("shows 'You Lose!' for a lose result", () => {
    renderWithContext(<ResultCard result={makeResult("lose", 1, 2)} />);
    expect(screen.getByText("You Lose!")).toBeInTheDocument();
  });

  it("shows \"It's a Tie!\" for a tie result", () => {
    renderWithContext(<ResultCard result={makeResult("tie", 1, 1)} />);
    expect(screen.getByText("It's a Tie!")).toBeInTheDocument();
  });

  it("displays the player and computer choice names", () => {
    renderWithContext(<ResultCard result={makeResult("win", 2, 1)} />);
    expect(screen.getByText("paper")).toBeInTheDocument();
    expect(screen.getByText("rock")).toBeInTheDocument();
  });

  it("applies the correct result modifier class", () => {
    const { container } = renderWithContext(<ResultCard result={makeResult("lose", 1, 2)} />);
    expect(container.firstChild).toHaveClass("result--lose");
  });

  it("shows fallback icon for unknown choice ids", () => {
    renderWithContext(<ResultCard result={makeResult("win", 99, 99)} />);
    expect(screen.getAllByText("❓")).toHaveLength(2);
  });
});
