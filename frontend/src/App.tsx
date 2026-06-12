import "./App.css";
import { useGame } from "./hooks/useGame";
import { ChoiceGrid, ChoiceSkeleton, ErrorBoundary, ResultCard, Scoreboard } from "./components";
import { ChoiceProvider } from "./context/ChoiceContext";

export default function App() {
  const { choices, result, scoreboard, status, error, choiceMap, play, reset } = useGame();

  const initializing = status === "initializing";
  const busy = status === "busy";

  return (
    <ErrorBoundary>
      <ChoiceProvider choiceMap={choiceMap}>
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

          {result && !busy && <ResultCard result={result} />}

          <Scoreboard
            scoreboard={scoreboard}
            disabled={busy}
            onReset={reset}
          />
        </div>
      </ChoiceProvider>
    </ErrorBoundary>
  );
}
