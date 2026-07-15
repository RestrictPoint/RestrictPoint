import { Title1 } from "@fluentui/react-components";
import type { ReactElement } from "react";

export interface PageHeaderProps {
  /** Page title rendered as the top-level heading. */
  title: string;
}

/**
 * Accessible page header used across all RestrictPoint frontends.
 */
export function PageHeader({ title }: PageHeaderProps): ReactElement {
  return (
    <header>
      <Title1 as="h1">{title}</Title1>
    </header>
  );
}
