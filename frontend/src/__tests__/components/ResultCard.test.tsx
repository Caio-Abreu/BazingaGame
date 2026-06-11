import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ResultCard } from "../../components/ResultCard";
import type { ScoredResult } from "../../types/game";

const choiceMap: Record<string, string> = {
  "1": "rock",
  "2": "paper",
  "3": "scissors",
};

function makeResult(result: ScoredResult["results"], player: number, computer: number): ScoredResult {
  return { id: "test-id", results: result, player, computer };
}

describe("ResultCard", () => {
  it("shows 'You Win!' for a win result", () => {
    render(<ResultCard result={makeResult("win", 2, 1)} choiceMap={choiceMap} />);
    expect(screen.getByText("You Win!")).toBeInTheDocument();
  });

  it("shows 'You Lose!' for a lose result", () => {
    render(<ResultCard result={makeResult("lose", 1, 2)} choiceMap={choiceMap} />);
    expect(screen.getByText("You Lose!")).toBeInTheDocument();
  });

  it("shows \"It's a Tie!\" for a tie result", () => {
    render(<ResultCard result={makeResult("tie", 1, 1)} choiceMap={choiceMap} />);
    expect(screen.getByText("It's a Tie!")).toBeInTheDocument();
  });

  it("displays the player and computer choice names", () => {
    render(<ResultCard result={makeResult("win", 2, 1)} choiceMap={choiceMap} />);
    expect(screen.getByText("paper")).toBeInTheDocument();
    expect(screen.getByText("rock")).toBeInTheDocument();
  });

  it("applies the correct result modifier class", () => {
    const { container } = render(<ResultCard result={makeResult("lose", 1, 2)} choiceMap={choiceMap} />);
    expect(container.firstChild).toHaveClass("result--lose");
  });

  it("shows fallback icon for unknown choice ids", () => {
    render(<ResultCard result={makeResult("win", 99, 99)} choiceMap={choiceMap} />);
    expect(screen.getAllByText("❓")).toHaveLength(2);
  });
});
