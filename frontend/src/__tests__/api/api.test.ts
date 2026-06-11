import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { api } from "../../api/gameApi";

const PLAYER_ID = "test-player-uuid";

beforeEach(() => {
  vi.stubGlobal("crypto", { randomUUID: () => PLAYER_ID });
  localStorage.clear();
});

afterEach(() => {
  vi.restoreAllMocks();
});

function mockFetch(body: unknown, status = 200) {
  return vi.spyOn(globalThis, "fetch").mockResolvedValueOnce({
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
  } as Response);
}

describe("api.getChoices", () => {
  it("calls GET /choices and returns the list", async () => {
    const choices = [{ id: 1, name: "rock" }];
    const spy = mockFetch(choices);

    const result = await api.getChoices();

    expect(spy).toHaveBeenCalledWith(
      expect.stringContaining("/choices"),
      expect.objectContaining({ headers: expect.objectContaining({ "X-Player-Id": expect.any(String) }) })
    );
    expect(result).toEqual(choices);
  });
});

describe("api.play", () => {
  it("calls POST /play with the player choice", async () => {
    const playResult = { results: "win", player: 1, computer: 3 };
    const spy = mockFetch(playResult);

    const result = await api.play(1);

    expect(spy).toHaveBeenCalledWith(
      expect.stringContaining("/play"),
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ player: 1 }),
      })
    );
    expect(result).toEqual(playResult);
  });
});

describe("api.getScoreboard", () => {
  it("calls GET /scoreboard and returns the list", async () => {
    const board = [{ results: "lose", player: 2, computer: 1 }];
    const spy = mockFetch(board);

    const result = await api.getScoreboard();

    expect(spy).toHaveBeenCalledWith(
      expect.stringContaining("/scoreboard"),
      expect.anything()
    );
    expect(result).toEqual(board);
  });
});

describe("api.resetScoreboard", () => {
  it("calls DELETE /scoreboard and returns undefined on 204", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValueOnce({
      ok: true,
      status: 204,
      json: () => Promise.resolve({}),
    } as Response);

    const result = await api.resetScoreboard();

    expect(result).toBeUndefined();
  });
});

describe("api error handling", () => {
  it("throws an error with the server message on non-ok response", async () => {
    mockFetch({ error: "Rate limit exceeded" }, 429);

    await expect(api.getChoices()).rejects.toThrow("Rate limit exceeded");
  });

  it("throws a fallback error when the body has no error field", async () => {
    mockFetch({}, 500);

    await expect(api.getChoices()).rejects.toThrow("Request failed (500)");
  });
});

describe("player identity", () => {
  it("persists the same player id across multiple calls", async () => {
    mockFetch([]);
    mockFetch([]);

    await api.getChoices();
    await api.getChoices();

    const storedId = localStorage.getItem("bazinga_player_id");
    expect(storedId).toBeTruthy();
  });
});
