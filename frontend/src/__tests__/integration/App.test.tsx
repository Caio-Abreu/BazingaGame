import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import App from "../../App";
import { api } from "../../api/gameApi";

vi.mock("../../api/gameApi", () => ({
  api: {
    getChoices: vi.fn(),
    getScoreboard: vi.fn(),
    play: vi.fn(),
    resetScoreboard: vi.fn(),
  },
}));

const choices = [
  { id: 1, name: "rock" },
  { id: 2, name: "paper" },
  { id: 3, name: "scissors" },
  { id: 4, name: "lizard" },
  { id: 5, name: "spock" },
];

beforeEach(() => {
  vi.mocked(api.getChoices).mockResolvedValue(choices);
  vi.mocked(api.getScoreboard).mockResolvedValue([]);
});

afterEach(() => {
  vi.clearAllMocks();
});

describe("App — loading state", () => {
  it("shows skeleton buttons while initializing", () => {
    vi.mocked(api.getChoices).mockReturnValue(new Promise(() => {}));
    const { container } = render(<App />);
    expect(container.querySelectorAll(".choice-btn--skeleton")).toHaveLength(5);
  });
});

describe("App — ready state", () => {
  it("renders all 5 choice buttons after loading", async () => {
    render(<App />);
    await waitFor(() => expect(screen.getByText("rock")).toBeInTheDocument());
    expect(screen.getAllByRole("button", { name: /rock|paper|scissors|lizard|spock/i })).toHaveLength(5);
  });

  it("shows error banner when API fails to load", async () => {
    vi.mocked(api.getChoices).mockRejectedValueOnce(new Error("network"));
    render(<App />);
    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(/could not connect/i)
    );
  });
});

describe("App — playing a round", () => {
  it("shows the result card after clicking a choice", async () => {
    vi.mocked(api.play).mockResolvedValueOnce({ results: "win", player: 1, computer: 3 });
    render(<App />);
    await waitFor(() => screen.getByText("rock"));

    await userEvent.click(screen.getByText("rock"));

    await waitFor(() => expect(screen.getByText("You Win!")).toBeInTheDocument());
    expect(screen.getByText(/you chose/i)).toBeInTheDocument();
  });

  it("adds the result to the scoreboard", async () => {
    vi.mocked(api.play).mockResolvedValueOnce({ results: "lose", player: 2, computer: 1 });
    render(<App />);
    await waitFor(() => screen.getByText("paper"));

    await userEvent.click(screen.getByText("paper"));

    await waitFor(() => expect(screen.getByText("LOSE")).toBeInTheDocument());
  });

  it("shows an error banner on play failure", async () => {
    vi.mocked(api.play).mockRejectedValueOnce(new Error("server exploded"));
    render(<App />);
    await waitFor(() => screen.getByText("rock"));

    await userEvent.click(screen.getByText("rock"));

    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent("server exploded")
    );
  });
});

describe("App — reset", () => {
  it("clears result and scoreboard after reset", async () => {
    vi.mocked(api.play).mockResolvedValueOnce({ results: "win", player: 1, computer: 3 });
    vi.mocked(api.resetScoreboard).mockResolvedValueOnce(undefined);
    render(<App />);
    await waitFor(() => screen.getByText("rock"));

    await userEvent.click(screen.getByText("rock"));
    await waitFor(() => screen.getByText("You Win!"));

    await userEvent.click(screen.getByRole("button", { name: /reset/i }));

    await waitFor(() => {
      expect(screen.queryByText("You Win!")).not.toBeInTheDocument();
      expect(screen.getByText("No games played yet.")).toBeInTheDocument();
    });
  });
});
