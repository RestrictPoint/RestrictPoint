import { FluentProvider, Title1, webLightTheme } from "@fluentui/react-components";
import type { ReactElement } from "react";

export function App(): ReactElement {
  return (
    <FluentProvider theme={webLightTheme}>
      <main>
        <Title1 as="h1">RestrictPoint Developer Portal</Title1>
      </main>
    </FluentProvider>
  );
}
