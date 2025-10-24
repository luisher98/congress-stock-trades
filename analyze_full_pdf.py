#!/usr/bin/env python3

import pdfplumber
import json
import re
from collections import defaultdict

def extract_all_committee_data(pdf_path):
    """Extract all committee assignments from the PDF for comparison."""

    committees = defaultdict(list)
    subcommittees = defaultdict(lambda: defaultdict(list))
    all_members = set()
    member_assignments = defaultdict(list)

    current_committee = None
    current_subcommittee = None
    in_subcommittee_section = False
    is_majority = True

    with pdfplumber.open(pdf_path) as pdf:
        for page_num, page in enumerate(pdf.pages, 1):
            text = page.extract_text()
            if not text:
                continue

            lines = text.split('\n')

            for line in lines:
                line = line.strip()

                if not line:
                    continue

                # Check for subcommittee section header
                if re.match(r'SUBCOMMITTEES?\s+OF\s+THE\s+COMMITTEE\s+ON', line, re.IGNORECASE):
                    in_subcommittee_section = True
                    match = re.search(r'ON\s+(.+)$', line, re.IGNORECASE)
                    if match:
                        current_committee = match.group(1).strip()
                        current_subcommittee = None
                    continue

                # Check for main committee headers (all caps, but not subcommittee headers)
                if line.isupper() and len(line) > 3 and not line.startswith('SUBCOMMITTEE'):
                    # Skip common headers
                    skip_patterns = [
                        'STANDING COMMITTEES', 'SELECT COMMITTEES', 'JOINT COMMITTEES',
                        'ALPHABETICAL LIST', 'HOUSE OF REPRESENTATIVES', 'ONE HUNDRED',
                        'CONGRESS', 'MAJORITY', 'MINORITY', 'DEMOCRATS', 'REPUBLICANS',
                        'RATIO', 'WASHINGTON', 'CONTENTS', 'PREPARED UNDER'
                    ]

                    if any(pattern in line for pattern in skip_patterns):
                        if 'MAJORITY' in line:
                            is_majority = True
                        elif 'MINORITY' in line:
                            is_majority = False
                        continue

                    # This could be a committee or subcommittee name
                    if in_subcommittee_section and current_committee:
                        # It's a subcommittee
                        current_subcommittee = line
                    else:
                        # It's a main committee
                        current_committee = line
                        current_subcommittee = None
                        in_subcommittee_section = False
                        is_majority = True
                    continue

                # Parse member lines
                # Pattern 1: Numbered members (e.g., "3. Pete Sessions, TX")
                numbered_pattern = r'(\d+)\.\s*([A-Za-z\s\.\-\']+?),\s*([A-Z]{2})'
                matches = re.findall(numbered_pattern, line)

                if matches:
                    for match in matches:
                        position, name, state = match
                        name = re.sub(r'\s+', ' ', name).strip()
                        member_key = f"{name}, {state}"
                        all_members.add(member_key)

                        assignment = {
                            'committee': current_committee,
                            'subcommittee': current_subcommittee,
                            'position': int(position),
                            'page': page_num,
                            'group': 'Majority' if is_majority else 'Minority',
                            'raw_line': line
                        }

                        member_assignments[member_key].append(assignment)

                        if current_subcommittee:
                            subcommittees[current_committee][current_subcommittee].append(member_key)
                        else:
                            committees[current_committee].append(member_key)

                # Pattern 2: Non-numbered members in subcommittees
                # (e.g., "Pete Sessions, TX Juan Vargas, CA")
                if current_subcommittee and not matches:
                    non_numbered_pattern = r'([A-Z][A-Za-z\s\.\-\']+?),\s*([A-Z]{2})(?:\s*,\s*[A-Za-z]+)?'
                    non_numbered_matches = re.findall(non_numbered_pattern, line)

                    if non_numbered_matches:
                        for i, (name, state) in enumerate(non_numbered_matches):
                            name = re.sub(r'\s+', ' ', name).strip()
                            # Fix concatenated names
                            name = re.sub(r'([a-z])([A-Z])', r'\1 \2', name)
                            member_key = f"{name}, {state}"
                            all_members.add(member_key)

                            assignment = {
                                'committee': current_committee,
                                'subcommittee': current_subcommittee,
                                'position': 0,  # No position for subcommittee members
                                'page': page_num,
                                'group': 'Majority' if i == 0 else 'Minority',
                                'raw_line': line
                            }

                            member_assignments[member_key].append(assignment)
                            subcommittees[current_committee][current_subcommittee].append(member_key)

    return {
        'committees': dict(committees),
        'subcommittees': {k: dict(v) for k, v in subcommittees.items()},
        'members': sorted(list(all_members)),
        'member_assignments': dict(member_assignments)
    }

def main():
    pdf_path = '/Users/Luis/dev/congress-stock-trades/examples/scsoal.pdf'

    print("Extracting all committee data from PDF...")
    print("=" * 80)

    data = extract_all_committee_data(pdf_path)

    # Statistics
    print(f"\nStatistics:")
    print(f"  Total Committees: {len(data['committees'])}")
    print(f"  Total Subcommittees: {sum(len(subs) for subs in data['subcommittees'].values())}")
    print(f"  Total Members: {len(data['members'])}")
    print(f"  Total Assignments: {sum(len(assignments) for assignments in data['member_assignments'].values())}")

    # Find Pete Sessions
    pete_sessions_assignments = []
    for member, assignments in data['member_assignments'].items():
        if 'Sessions' in member and 'Pete' in member:
            pete_sessions_assignments.extend(assignments)

    print(f"\n{'=' * 80}")
    print(f"PETE SESSIONS ANALYSIS")
    print(f"{'=' * 80}")
    print(f"Total Pete Sessions Assignments Found: {len(pete_sessions_assignments)}")

    for i, assignment in enumerate(sorted(pete_sessions_assignments, key=lambda x: x['page']), 1):
        print(f"\n{i}. Page {assignment['page']}:")
        print(f"   Committee: {assignment['committee']}")
        if assignment['subcommittee']:
            print(f"   Subcommittee: {assignment['subcommittee']}")
        else:
            print(f"   Type: Main Committee")
        print(f"   Group: {assignment['group']}")
        if assignment['position'] > 0:
            print(f"   Position: #{assignment['position']}")
        print(f"   Raw: \"{assignment['raw_line'][:80]}...\"" if len(assignment['raw_line']) > 80
              else f"   Raw: \"{assignment['raw_line']}\"")

    # Save detailed results to JSON
    output_file = '/Users/Luis/dev/congress-stock-trades/pdf_analysis_results.json'
    with open(output_file, 'w') as f:
        json.dump(data, f, indent=2)
    print(f"\n\nDetailed results saved to: {output_file}")

if __name__ == "__main__":
    main()