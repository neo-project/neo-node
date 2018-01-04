### Test & Continuous Integration Setup

The test suite can be run manually, and is automatically run by [Travis CI](https://travis-ci.org/neo-project/neo-cli) on each code push.

To run the tests manually, you need to install [Docker CE](https://www.docker.com/community-edition#/download), and run the test script from a bash compatible shell (eg. Git bash on Windows) like this:

    ./ci/build-and-test.sh

The test suite performs the following tasks:

* Build the latest code
* Verify the basic neo-cli functionality using [expect](https://linux.die.net/man/1/expect)
* Verify JSON-RPC functionality with curl

Files:

* `Dockerfile`: the system to build neo-cli and to run the tests
* `build-and-test.sh`: this builds the Docker image, starts it and runs the tests inside. This is useful for testing the CI run on a local dev machine.
* `run-tests-in-docker.sh`: is run inside the Docker container and executes the tests
* `test-neo-cli.expect`: [expect](https://linux.die.net/man/1/expect) script which verifies neo-cli functionality
