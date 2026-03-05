# Git Commit Message Guidelines

This document curates and distills conventional commit prefixes into 7 essential types based on our core design philosophy. The goal is to eliminate decision fatigue and accelerate the workflow on the development floor.

## 1. Design Philosophy (The "Why")

The primary purpose of categorizing with prefixes is to clearly delineate whether a change is **outward-facing** (affecting the user) or **inward-facing** (affecting the code structure or environment).
This binary distinction significantly enhances the searchability of our Git history and allows developers to immediately assess the impact radius of any given commit.

## 2. Defined Prefixes

In practice, appropriately selecting from the following 7 prefixes covers the entirety of our development tasks.

| Prefix | Scope | Nature of Change | Examples |
| --- | --- | --- | --- |
| **feat** | User-Facing | Feature addition/modification | Implementing a new feature, changing existing feature specs, UI updates |
| **fix** | User-Facing | Bug fixes | Patching bugs, fixing typos (only if they affect the user display) |
| **refactor** | Internal | Structural improvements | Restructuring logic, renaming variables, removing redundant code |
| **docs** | Documentation | Documents | Updating README, adding/revising inline code comments |
| **style** | Internal Quality | Formatting adjustments | Adjusting indentation/line breaks, adding missing semicolons |
| **test** | Internal Quality | Testing | Authoring new test codes, modifying existing tests |
| **chore** | Environment/Tasks | Miscellaneous | Updating libraries, modifying build configurations, deleting unused files |

## 3. Decision Matrix for Ambiguous Cases

When encountering a change that feels difficult to categorize, follow this priority matrix to make a definitive choice.

| Decision Axis | Evaluation Criteria | Recommended Prefix |
| --- | --- | --- |
| **User Perception** | Does the change update the user experience or alter system behavior? | **feat** |
| **Internal Quality** | Did readability or maintainability improve without altering behavior? | **refactor** |
| **Environment/Maintenance** | Is this a tool configuration or file cleanup rather than feature development? | **chore** |
| **Documentation/Formatting** | Is the change purely text or visual formatting with zero impact on logic? | **docs** / **style** |

## 4. Formatting Guidelines

Commit messages must be concise and precise.

| Component | Rule | Note |
| --- | --- | --- |
| **Line 1 (Summary)** | Keep it brief (approx. 50 chars) | Do not use punctuation at the end. Always insert a space after the prefix. |
| **Line 2 (Blank Line)** | Must be left blank | Crucial for readability in Git tools and platforms (e.g., GitHub). |
| **Line 3+ (Details)** | Bullet points are recommended | Avoid repetitive conjunctions; ensure logical flow and clarity. |
| **Symbol Usage** | Keep to an absolute minimum | Use only for quotes/references. Avoid overuse for emphasis. |

### Example

```
feat: add filter options to user search functionality

- Introduce search refinement via date range selection
- Implement a date picker calendar component in the UI
- Strengthen validation logic for search parameters

```