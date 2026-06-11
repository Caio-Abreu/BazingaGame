import "./App.css";
import { useGame } from "./hooks/useGame";
import { ChoiceGrid, ChoiceSkeleton, ResultCard, Scoreboard } from "./components";

export default function App() {
  const { choices, result, scoreboard, status, error, choiceMap, play, reset } = useGame();

  const initializing = status === "initializing";
  const busy = status === "busy";

  return (
    <div className="app">
      <h1>Rock, Paper, Scissors, Lizard, Spock</h1>

      {error && (
        <div className="error-banner" role="alert">
          {error}
        </div>
      )}

      {initializing ? (
        <ChoiceSkeleton />
      ) : (
        <ChoiceGrid choices={choices} disabled={busy} onSelect={play} />
      )}

      {busy && <p className="status">Waiting for computer...</p>}

      {result && !busy && <ResultCard result={result} choiceMap={choiceMap} />}

      <Scoreboard
        scoreboard={scoreboard}
        disabled={busy}
        onReset={reset}
        choiceMap={choiceMap}
      />
    </div>
  );
}
