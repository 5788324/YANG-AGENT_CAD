from yang_cad_agent.doctor import run_doctor


def test_doctor_returns_expected_sections():
    result = run_doctor()
    assert "python" in result
    assert "git" in result
    assert "accoreconsole" in result

