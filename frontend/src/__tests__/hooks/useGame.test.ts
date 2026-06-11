import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { useGame } from "../../hooks/useGame";
import { api } from "../../api/gameApi";

vi.mock("../../api/gameApi", () => ({
  api: {
    getChoices: vi.fn(),
    getScoreboard: vi.fn(),
    play: vi.fn(),
    resetScoreboard: vi.fn(),
  },
}));

const mockChoices = [
  { id: 1, name: "rock" },
  { id: 2, name: "paper" },
  { id: 3, name: "scissors" },
  { id: 4, name: "lizard" },
  { id: 5, name: "spock" },
];

const mockPlayResult = { results: "win" as const, player: 1, computer: 3 };

beforeEach(() => {
  vi.mocked(api.getChoices).mockResolvedValue(mockChoices);
  vi.mocked(api.getScoreboard).mockResolvedValue([]);
});

afterEach(() => {
  vi.clearAllMocks();
});

describe("useGame — initialisation", () => {
  it("starts in initializing status", () => {
    const { result } = renderHook(() => useGame());
    expect(result.current.status).toBe("initializing");
  });

  it("transitions to idle after loading choices and scoreboard", async () => {
    const { result } = renderHook(() => useGame());
    await waitFor(() => expect(result.current.status).toBe("idle"));
    expect(result.current.choices).toEqual(mockChoices);
    expect(result.current.scoreboard).toHaveLength(0);
  });

  it("builds choiceMap keyed by string id", async () => {
    const { result } = renderHook(() => useGame());
    await waitFor(() => expect(result.current.status).toBe("idle"));
    expect(result.current.choiceMap["1"]).toBe("rock");
    expect(result.current.choiceMap["5"]).toBe("spock");
  });

  it("sets error when the API fails on init", async () => {
    vi.mocked(api.getChoices).mockRejectedValueOnce(new Error("network"));
    const { result } = renderHook(() => useGame());
    await waitFor(() => expect(result.current.status).toBe("idle"));
    expect(result.current.error).toMatch(/could not connect/i);
  });

  it("loads existing scoreboard entries with stable ids", async () => {
    vi.mocked(api.getScoreboard).mockResolvedValueOnce([mockPlayResult]);
    const { result } = renderHook(() => useGame());
    await waitFor(() => expect(result.current.status).toBe("idle"));
    expect(result.current.scoreboard).toHaveLength(1);
    expect(result.current.scoreboard[0].id).toBeTruthy();
  });
});

describe("useGame — play", () => {
  it("sets status to busy while waiting, then idle", async () => {
    vi.mocked(api.play).mockResolvedValueOnce(mockPlayResult);
    const { result } = renderHook(() => useGame());
    await waitFor(() => expect(result.current.status).toBe("idle"));

    act(() => { result.current.play(1); });
    expect(result.current.status).toBe("busy");

    await waitFor(() => expect(result.current.status).toBe("idle"));
  });

  it("updates result and prepends to scoreboard after a play", async () => {
    vi.mocked(api.play).mockResolvedValueOnce(mockPlayResult);
    const { result } = renderHook(() => useGame());
    await waitFor(() => expect(result.current.status).toBe("idle"));

    await act(() => result.current.play(1));

    expect(result.current.result?.results).toBe("win");
    expect(result.current.scoreboard).toHaveLength(1);
    expect(result.current.scoreboard[0].results).toBe("win");
  });

  it("caps scoreboard at 10 entries", async () => {
    vi.mocked(api.getScoreboard).mockResolvedValueOnce(
      Array.from({ length: 10 }, () => mockPlayResult)
    );
    vi.mocked(api.play).mockResolvedValueOnce(mockPlayResult);
    const { result } = renderHook(() => useGame());
    await waitFor(() => expect(result.current.status).toBe("idle"));

    await act(() => result.current.play(1));

    expect(result.current.scoreboard).toHaveLength(10);
  });

  it("sets error on play failure", async () => {
    vi.mocked(api.play).mockRejectedValueOnce(new Error("server error"));
    const { result } = renderHook(() => useGame());
    await waitFor(() => expect(result.current.status).toBe("idle"));

    await act(() => result.current.play(1));

    expect(result.current.error).toBe("server error");
    expect(result.current.status).toBe("idle");
  });
});

describe("useGame — reset", () => {
  it("clears scoreboard and result after reset", async () => {
    vi.mocked(api.play).mockResolvedValueOnce(mockPlayResult);
    vi.mocked(api.resetScoreboard).mockResolvedValueOnce(undefined);
    const { result } = renderHook(() => useGame());
    await waitFor(() => expect(result.current.status).toBe("idle"));

    await act(() => result.current.play(1));
    expect(result.current.scoreboard).toHaveLength(1);

    await act(() => result.current.reset());
    expect(result.current.scoreboard).toHaveLength(0);
    expect(result.current.result).toBeNull();
  });

  it("sets error on reset failure", async () => {
    vi.mocked(api.resetScoreboard).mockRejectedValueOnce(new Error("fail"));
    const { result } = renderHook(() => useGame());
    await waitFor(() => expect(result.current.status).toBe("idle"));

    await act(() => result.current.reset());

    expect(result.current.error).toMatch(/reset/i);
  });
});
