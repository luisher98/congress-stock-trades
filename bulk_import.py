#!/usr/bin/env python3
"""
Bulk import script for processing historical congressional filing PDFs.
Sends local PDFs to the Azure Function bulk import endpoint.
"""

import os
import json
import requests
from pathlib import Path

# Configuration
FUNCTION_URL = "http://localhost:7071/api/bulk-import"  # Change to Azure URL for production
PDF_DIRECTORY = "./pdfs"  # Directory containing your downloaded PDFs
BASE_URL = "https://disclosures-clerk.house.gov/public_disc/ptr-pdfs/2025/"

def get_pdf_files(directory):
    """Get all PDF files from directory."""
    pdf_path = Path(directory)
    if not pdf_path.exists():
        print(f"Error: Directory {directory} does not exist")
        return []

    return list(pdf_path.glob("*.pdf"))

def create_bulk_request(pdf_files):
    """Create bulk import request payload."""
    filings = []

    for pdf_file in pdf_files:
        filing_id = pdf_file.stem  # Filename without extension (e.g., "20033318")

        filings.append({
            "filingId": filing_id,
            "pdfUrl": f"{BASE_URL}{filing_id}.pdf",  # Use official URL
            "name": "Bulk Import",
            "office": "Unknown"
        })

    return {"filings": filings}

def submit_bulk_import(request_data, batch_size=10):
    """Submit filings in batches to avoid overwhelming the system."""
    filings = request_data["filings"]
    total = len(filings)

    print(f"Processing {total} filings in batches of {batch_size}...")

    for i in range(0, total, batch_size):
        batch = filings[i:i + batch_size]
        batch_request = {"filings": batch}

        print(f"\nSubmitting batch {i//batch_size + 1} ({len(batch)} filings)...")

        try:
            response = requests.post(
                FUNCTION_URL,
                json=batch_request,
                headers={"Content-Type": "application/json"},
                timeout=30
            )

            if response.status_code == 200:
                result = response.json()
                print(f"✓ Queued: {result['queued']}/{result['total']}")
                if result.get('errors'):
                    print(f"  Errors: {result['errors']}")
            else:
                print(f"✗ Failed: {response.status_code} - {response.text}")

        except Exception as e:
            print(f"✗ Exception: {str(e)}")

def main():
    """Main entry point."""
    print("Congressional Filing Bulk Import")
    print("=" * 50)

    # Get PDF files
    pdf_files = get_pdf_files(PDF_DIRECTORY)

    if not pdf_files:
        print(f"No PDF files found in {PDF_DIRECTORY}")
        return

    print(f"Found {len(pdf_files)} PDF files")

    # Create request
    request_data = create_bulk_request(pdf_files)

    # Confirm
    response = input(f"\nSubmit {len(pdf_files)} filings for processing? (yes/no): ")
    if response.lower() not in ['yes', 'y']:
        print("Cancelled")
        return

    # Submit
    submit_bulk_import(request_data, batch_size=10)

    print("\n" + "=" * 50)
    print("Bulk import complete!")
    print("Check Azure Function logs for processing status")

if __name__ == "__main__":
    main()
