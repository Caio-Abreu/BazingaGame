import "./ChoiceSkeleton.css";

export function ChoiceSkeleton() {
  return (
    <div className="choices">
      {["rock", "paper", "scissors", "lizard", "spock"].map((name) => (
        <div key={name} className="choice-btn--skeleton" />
      ))}
    </div>
  );
}
